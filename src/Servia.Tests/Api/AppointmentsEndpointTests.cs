using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Servia.Api.Contracts;
using Servia.Api.Middleware;
using Servia.Domain.Tenancy;
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
public class AppointmentsEndpointTests : IClassFixture<AppointmentsEndpointTests.Factory>
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid ServiceId = Guid.NewGuid();
    private static readonly Guid ProfessionalId = Guid.NewGuid();
    private static readonly DateOnly Date = new(2026, 11, 9);

    private readonly Factory _factory;
    private readonly HttpClient _client;

    public AppointmentsEndpointTests(Factory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static CreateAppointmentRequest Request(TimeOnly time, int duration = 30) =>
        new(CustomerId, ServiceId, ProfessionalId, Date, time, duration);

    /// <summary>Envia o pedido com o tenant resolvido pelo header do servidor.</summary>
    private async Task<HttpResponseMessage> PostAsync(
        CreateAppointmentRequest request,
        Guid? tenantId = null)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/appointments")
        {
            Content = JsonContent.Create(request)
        };

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
            Guid.Empty, ServiceId, ProfessionalId, Date, new TimeOnly(11, 0), 30);

        var response = await PostAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Isolamento ----------

    [Fact]
    public async Task Agendamento_De_Outro_Tenant_Nao_Bloqueia_Horario()
    {
        var time = new TimeOnly(15, 0);

        var otherTenant = new CreateAppointmentRequest(
            CustomerId, ServiceId, ProfessionalId, Date, time, 60);

        await PostAsync(otherTenant, Guid.NewGuid());

        var mine = await PostAsync(Request(time, 60));

        Assert.Equal(HttpStatusCode.Created, mine.StatusCode);
    }

    // ---------- Fábrica com banco isolado ----------

    /// <summary>
    /// Substitui o contexto registrado pela aplicação por um banco SQLite
    /// em memória exclusivo desta classe de testes.
    /// </summary>
    public sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly string _databasePath =
            Path.Combine(Path.GetTempPath(), $"servia-tests-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.UseEnvironment("Development");

            builder.ConfigureServices(services =>
            {
                var descriptors = services
                    .Where(descriptor => descriptor.ServiceType == typeof(DbContextOptions<AppDbContext>))
                    .ToList();

                foreach (var descriptor in descriptors)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlite($"Data Source={_databasePath}"));
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!disposing)
            {
                return;
            }

            // Libera o pool de conexoes antes de remover o arquivo.
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            try
            {
                if (File.Exists(_databasePath))
                {
                    File.Delete(_databasePath);
                }
            }
            catch (IOException)
            {
                // Arquivo temporario: a remocao e best-effort.
            }
        }
    }
}
