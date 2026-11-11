using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Servia.Api.Contracts;
using Servia.Api.Middleware;
using Servia.Domain.Appointments;
using Servia.Domain.Catalog;
using Servia.Domain.Common;
using Servia.Domain.Customers;
using Servia.Domain.Team;
using Servia.Domain.Identity;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Identity;
using Servia.Infrastructure.Persistence;

namespace Servia.Tests.Api;

/// <summary>
/// Teste de integração ponta a ponta do fluxo F1 (criar agendamento).
///
/// Exercita o caminho completo: HTTP → controller → caso de uso → domínio →
/// persistência. Usa um banco SQLite isolado por teste, substituindo o
/// contexto registrado pela aplicação.
///
/// Critério F03: teste de integração que executa uma operação pela API,
/// aplica uma regra de negócio e verifica a persistência.
/// </summary>
public class AppointmentsEndpointTests : IClassFixture<IsolatedApiFactory>
{
    private static readonly Guid TenantId = Guid.NewGuid();
    // IDs reais das entidades criadas no banco de teste.
    private const string Password = "SenhaSegura123";

    private readonly string _accessToken;

    private Guid _customerId;
    private Guid _serviceId;
    private Guid _professionalId;
    private static readonly DateOnly Date = new(2026, 11, 9);

    private readonly IsolatedApiFactory _factory;
    private readonly HttpClient _client;

    public AppointmentsEndpointTests(IsolatedApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();

        SeedReferences();
        _accessToken = SignIn();
    }

    /// <summary>
    /// Cria o usuario proprietario e obtem o token.
    /// O endpoint de agendamento exige autenticacao e permissao (F10).
    /// </summary>
    private string SignIn()
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(TenantId);

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        if (!context.Users.Any())
        {
            context.Users.Add(User.CreateForTenant(
                TenantId, "Proprietario", "owner@exemplo.com",
                hasher.Hash(Password), Role.Owner, DateTimeOffset.UtcNow));

            context.SaveChanges();
        }

        var response = _client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(
            TenantId, "owner@exemplo.com", Password)).GetAwaiter().GetResult();

        var body = response.Content.ReadFromJsonAsync<AuthResponse>().GetAwaiter().GetResult();

        return body!.AccessToken;
    }

    /// <summary>
    /// Cria cliente, servico e profissional do tenant de teste.
    /// A FK composta (TenantId, Id) exige que existam antes do agendamento.
    /// </summary>
    private void SeedReferences()
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(TenantId);
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (context.Customers.Any())
        {
            var existing = context.Customers.First();
            _customerId = existing.Id;
            _serviceId = context.Services.First().Id;
            _professionalId = context.Professionals.First().Id;
            return;
        }

        var customer = Customer.Create(TenantId, "Cliente", "21999990000", now: DateTimeOffset.UtcNow);
        var service = Service.Create(TenantId, "Servico", Money.From(50m), 30);
        var professional = Professional.Create(TenantId, "Profissional",
            WorkSchedule.Create([DayOfWeek.Monday], new TimeOnly(9, 0), new TimeOnly(18, 0)));

        context.Customers.Add(customer);
        context.Services.Add(service);
        context.Professionals.Add(professional);
        context.SaveChanges();

        _customerId = customer.Id;
        _serviceId = service.Id;
        _professionalId = professional.Id;
    }

    private CreateAppointmentRequest Request(TimeOnly time, int duration = 30) =>
        new(_customerId, _serviceId, _professionalId, Date, time, duration);

    /// <summary>Envia o pedido com o tenant resolvido pelo header do servidor.</summary>
    private async Task<HttpResponseMessage> PostAsync(
        CreateAppointmentRequest request,
        Guid? tenantId = null)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/appointments")
        {
            Content = JsonContent.Create(request)
        };

        message.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

        message.Headers.Add(TenantResolutionMiddleware.TenantHeaderName,
            (tenantId ?? TenantId).ToString());

        return await _client.SendAsync(message);
    }

    // ---------- F1-CA1: criação bem-sucedida ----------

    [Fact]
    public async Task Criar_Agendamento_Retorna_201_E_Persiste()
    {
        var response = await PostAsync(Request(new TimeOnly(10, 0)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AppointmentResponse>();

        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.Id);
        Assert.Equal("Confirmed", body.Status);
        Assert.Equal(Date, body.Date);
        Assert.Equal(new TimeOnly(10, 0), body.Time);

        // Verifica a persistência: o registro existe no banco.
        // O filtro global exige tenant resolvido também na verificação.
        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(TenantId);
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persisted = await context.Appointments.FirstOrDefaultAsync(item => item.Id == body.Id);

        Assert.NotNull(persisted);
        Assert.Equal(TenantId, persisted!.TenantId);
    }

    // ---------- F1-CA4: conflito de horário ----------

    [Fact]
    public async Task Criar_Agendamento_Conflitante_Retorna_409()
    {
        var first = await PostAsync(Request(new TimeOnly(14, 0), 60));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await PostAsync(Request(new TimeOnly(14, 30), 30));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var body = await second.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.Contains("conflita", body!.Message);
    }

    [Fact]
    public async Task Conflito_Nao_Persiste_Agendamento_Duplicado()
    {
        var time = new TimeOnly(16, 0);

        await PostAsync(Request(time, 60));
        await PostAsync(Request(time, 60));

        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(TenantId);
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var count = await context.Appointments.CountAsync(item => item.Time == time);

        Assert.Equal(1, count);
    }

    // ---------- Validação ----------

    [Fact]
    public async Task Criar_Com_Duracao_Invalida_Retorna_400()
    {
        var response = await PostAsync(Request(new TimeOnly(11, 0), 0));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Criar_Com_Referencia_Vazia_Retorna_400()
    {
        var request = new CreateAppointmentRequest(
            Guid.Empty, _serviceId, _professionalId, Date, new TimeOnly(11, 0), 30);

        var response = await PostAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Isolamento ----------

    [Fact]
    public async Task Header_De_Tenant_Forjado_Eh_Ignorado_Quando_Ha_Token()
    {
        // F10.5: o tenant vem da claim do token, nao do header.
        // Forjar o header nao muda o tenant efetivo da requisicao.
        var time = new TimeOnly(15, 0);

        var forged = await PostAsync(Request(time, 60), Guid.NewGuid());

        // A requisicao e aceita, mas no tenant do token — nao no forjado.
        Assert.Equal(HttpStatusCode.Created, forged.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(TenantId);

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var created = await context.Appointments.FirstOrDefaultAsync(item => item.Time == time);

        Assert.NotNull(created);
        Assert.Equal(TenantId, created!.TenantId);
    }

    // ---------- Fábrica com banco isolado ----------

    /// <summary>
    /// Substitui o contexto registrado pela aplicação por um banco SQLite
    /// em memória exclusivo desta classe de testes.
    /// </summary>
}
