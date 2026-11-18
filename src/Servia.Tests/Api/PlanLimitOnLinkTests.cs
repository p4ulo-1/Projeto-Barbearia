using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Servia.Api.Contracts;
using Servia.Api.Controllers;
using Servia.Domain.Appointments;
using Servia.Domain.Common;
using Servia.Domain.Customers;
using Servia.Domain.Identity;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Identity;
using Servia.Infrastructure.Persistence;

namespace Servia.Tests.Api;

/// <summary>
/// Limite do plano aplicado ao tornar um profissional elegível.
///
/// <b>Regra aprovada (ESCOPO_AV2 §2.3):</b> o limite é verificado na
/// transação que cria, reativa <b>ou torna elegível</b> o profissional. Um
/// profissional ativo sem serviço não consome vaga; ao receber o primeiro
/// serviço ativo, passa a consumir. Sem a verificação na associação, seria
/// possível contornar o plano cadastrando profissionais sem serviço e
/// associando-os depois.
/// </summary>
public class PlanLimitOnLinkTests : IClassFixture<IsolatedApiFactory>
{
    // Tenant proprio por instancia: o xUnit cria uma instancia por teste, e cada
    // teste precisa de uma vaga de plano intacta. Compartilhar o tenant faria
    // os testes consumirem a mesma vaga e interferirem entre si.
    private readonly Guid TenantId = Guid.NewGuid();
    private const string Password = "SenhaSegura123";

    private readonly IsolatedApiFactory _factory;
    private readonly HttpClient _client;
    private readonly string _token;

    public PlanLimitOnLinkTests(IsolatedApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();

        SeedTenantWithSingleSlotPlan();
        _token = SignIn();
    }

    /// <summary>
    /// Cria o tenant, o proprietário e uma assinatura no plano Essencial
    /// (3 profissionais). O teste consome as vagas via API.
    /// </summary>
    private void SeedTenantWithSingleSlotPlan()
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(TenantId);

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        if (context.Users.Any())
        {
            return;
        }

        context.Users.Add(User.CreateForTenant(
            TenantId, "Proprietario", "owner@exemplo.com",
            hasher.Hash(Password), Role.Owner, DateTimeOffset.UtcNow));

        // Plano com uma única vaga: torna o limite observável com poucos
        // profissionais. O código é único por instância, porque o código do
        // plano tem restrição de unicidade global.
        var plan = Plan.Create(
            $"LIM1-{TenantId.ToString("N")[..12]}", "Limite 1", Money.From(99m),
            includedProfessionals: 1,
            SiteCustomizationLevel.Basic,
            includedAiUnits: 0);

        context.Plans.Add(plan);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        context.Subscriptions.Add(
            Subscription.StartTrial(TenantId, plan.Id, today));

        context.SaveChanges();
    }

    private string SignIn()
    {
        var response = _client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(
            TenantId, "owner@exemplo.com", Password)).GetAwaiter().GetResult();

        var body = response.Content.ReadFromJsonAsync<AuthResponse>().GetAwaiter().GetResult();

        return body!.AccessToken;
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string url,
        object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        return await _client.SendAsync(request);
    }

    private async Task<Guid> CreateProfessionalAsync(string name)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/v1/team/professionals",
            new CreateProfessionalRequest(
                name,
                [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday],
                new TimeOnly(9, 0), new TimeOnly(18, 0)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var professional = await response.Content.ReadFromJsonAsync<ProfessionalResponse>();

        return professional!.Id;
    }

    private async Task<Guid> CreateServiceAsync(string name)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/v1/catalog/services",
            new CreateServiceRequest(name, 50m, 30));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var service = await response.Content.ReadFromJsonAsync<ServiceResponse>();

        return service!.Id;
    }

    private Task<HttpResponseMessage> LinkAsync(Guid professionalId, Guid serviceId) =>
        SendAsync(
            HttpMethod.Post,
            $"/api/v1/team/professionals/{professionalId}/services/{serviceId}",
            body: null);

    [Fact]
    public async Task Catalogo_Reativacao_Respeita_O_Limite_E_Preserva_O_Vinculo()
    {
        var first = await CreateProfessionalAsync("Primeiro");
        var second = await CreateProfessionalAsync("Segundo");
        var firstService = await CreateServiceAsync("Primeiro serviço");
        var secondService = await CreateServiceAsync("Segundo serviço");
        var firstRoute = $"/api/v1/catalog/services/{firstService}";
        var secondRoute = $"/api/v1/catalog/services/{secondService}";

        Assert.Equal(HttpStatusCode.NoContent, (await LinkAsync(first, firstService)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Post, firstRoute + "/deactivate")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await LinkAsync(second, secondService)).StatusCode);

        var rejected = await SendAsync(HttpMethod.Post, firstRoute + "/activate");
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Contains("upgrade", (await rejected.Content.ReadFromJsonAsync<ErrorResponse>())!.Message);
        var service = await SendAsync(HttpMethod.Get, firstRoute);
        Assert.False((await service.Content.ReadFromJsonAsync<ServiceResponse>())!.IsActive);
        var links = await SendAsync(HttpMethod.Get, $"/api/v1/team/professionals/{first}/services");
        Assert.Equal(firstService, Assert.Single((await links.Content.ReadFromJsonAsync<Guid[]>())!));

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Post, secondRoute + "/deactivate")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Post, firstRoute + "/activate")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Post, firstRoute + "/activate")).StatusCode);
    }

    [Fact]
    public async Task Catalogo_Correcao_De_Duracao_Acima_Do_Limite_Reverte_Toda_A_Edicao()
    {
        var first = await CreateProfessionalAsync("Ocupante");
        var second = await CreateProfessionalAsync("Cadastro legado");
        var firstService = await CreateServiceAsync("Serviço do ocupante");
        var legacy = await CreateServiceAsync("Duração legada");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"Services\" SET \"DurationMinutes\" = 0 WHERE \"Id\" = {legacy}");
        }
        Assert.Equal(HttpStatusCode.NoContent, (await LinkAsync(first, firstService)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await LinkAsync(second, legacy)).StatusCode);

        var route = $"/api/v1/catalog/services/{legacy}";
        var rejected = await SendAsync(HttpMethod.Put, route, new UpdateServiceRequest("Corrigido", 30));
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Contains("upgrade", (await rejected.Content.ReadFromJsonAsync<ErrorResponse>())!.Message);
        var detail = await SendAsync(HttpMethod.Get, route);
        var stored = (await detail.Content.ReadFromJsonAsync<ServiceResponse>())!;
        Assert.Equal("Duração legada", stored.Name);
        Assert.Equal(0, stored.DurationMinutes);
    }

    [Fact]
    public async Task Equipe_Reativacao_E_Remocao_De_Vinculo_Respeitam_Limite_E_Historico()
    {
        var first = await CreateProfessionalAsync("Primeiro");
        var second = await CreateProfessionalAsync("Segundo");
        var firstService = await CreateServiceAsync("Primeiro serviço");
        var secondService = await CreateServiceAsync("Segundo serviço");
        var firstRoute = $"/api/v1/team/professionals/{first}";
        var firstLink = $"{firstRoute}/services/{firstService}";
        var secondLink = $"/api/v1/team/professionals/{second}/services/{secondService}";
        Assert.Equal(HttpStatusCode.NoContent, (await LinkAsync(first, firstService)).StatusCode);

        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(TenantId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.UtcNow;
        var customer = Customer.Create(TenantId, "Cliente com histórico", "21999990000", now: now);
        var appointment = Appointment.Create(TenantId, customer.Id, firstService, first,
            DateOnly.FromDateTime(now.UtcDateTime), new TimeOnly(10, 0), 30, now);
        db.AddRange(customer, appointment);
        await db.SaveChangesAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Post, firstRoute + "/deactivate")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await LinkAsync(second, secondService)).StatusCode);
        var rejected = await SendAsync(HttpMethod.Post, firstRoute + "/activate");
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Contains("upgrade", (await rejected.Content.ReadFromJsonAsync<ErrorResponse>())!.Message);
        var detail = await SendAsync(HttpMethod.Get, firstRoute);
        Assert.False((await detail.Content.ReadFromJsonAsync<ProfessionalDetailResponse>())!.IsActive);
        var retained = await SendAsync(HttpMethod.Get, firstRoute + "/services");
        Assert.Equal(firstService, Assert.Single((await retained.Content.ReadFromJsonAsync<Guid[]>())!));

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Delete, firstLink)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Delete, firstLink)).StatusCode);
        var removed = await SendAsync(HttpMethod.Get, firstRoute + "/services");
        Assert.Empty((await removed.Content.ReadFromJsonAsync<Guid[]>())!);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Post, firstRoute + "/activate")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await LinkAsync(first, firstService)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Delete, secondLink)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await LinkAsync(first, firstService)).StatusCode);

        var saved = await db.Appointments.AsNoTracking().SingleAsync(item => item.Id == appointment.Id);
        Assert.Equal(first, saved.ProfessionalId);
        Assert.Equal(firstService, saved.ServiceId);
        Assert.Equal(AppointmentStatus.Confirmed, saved.Status);
        Assert.Equal(30, saved.DurationMinutes);
    }

    [Fact]
    public async Task Equipe_Reativacoes_Concorrentes_Disputam_A_Mesma_Vaga()
    {
        var first = await CreateProfessionalAsync("Primeiro");
        var second = await CreateProfessionalAsync("Segundo");
        var serviceId = await CreateServiceAsync("Serviço");
        var routes = new[] { first, second }.Select(id => $"/api/v1/team/professionals/{id}").ToArray();
        foreach (var route in routes)
        {
            Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Post, route + "/deactivate")).StatusCode);
        }
        Assert.Equal(HttpStatusCode.NoContent, (await LinkAsync(first, serviceId)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await LinkAsync(second, serviceId)).StatusCode);

        var results = await Task.WhenAll(routes.Select(route => SendAsync(HttpMethod.Post, route + "/activate")));
        Assert.Single(results, response => response.StatusCode == HttpStatusCode.NoContent);
        var rejected = Assert.Single(results, response => response.StatusCode != HttpStatusCode.NoContent);
        Assert.Contains(rejected.StatusCode, new[] { HttpStatusCode.BadRequest, HttpStatusCode.Conflict });

        var details = await Task.WhenAll(routes.Select(async route =>
            await (await SendAsync(HttpMethod.Get, route)).Content.ReadFromJsonAsync<ProfessionalDetailResponse>()));
        Assert.Single(details, item => item!.IsActive);
    }

    [Fact]
    public async Task Troca_Local_De_Plano_Aplica_Novo_Limite()
    {
        var code = $"LIM2-{TenantId.ToString("N")[..12]}";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Plans.Add(Plan.Create(code, "Limite 2", Money.From(199m), 2,
                SiteCustomizationLevel.Basic, includedAiUnits: 0));
            await db.SaveChangesAsync();
        }

        var service = await CreateServiceAsync("Serviço");
        var first = await CreateProfessionalAsync("Primeiro");
        var second = await CreateProfessionalAsync("Segundo");
        Assert.Equal(HttpStatusCode.NoContent, (await LinkAsync(first, service)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await LinkAsync(second, service)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Patch,
            "/api/v1/billing/subscription", new SubscribePlanRequest(code))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await LinkAsync(second, service)).StatusCode);
        var originalCode = $"LIM1-{TenantId.ToString("N")[..12]}";
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(HttpMethod.Patch,
            "/api/v1/billing/subscription", new SubscribePlanRequest(originalCode))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Post,
            $"/api/v1/catalog/services/{service}/deactivate")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Patch,
            "/api/v1/billing/subscription", new SubscribePlanRequest(originalCode))).StatusCode);
    }

    [Fact]
    public async Task Profissional_Sem_Servico_Nao_Consome_Vaga()
    {
        // Dois profissionais sem serviço: nenhum ocupa vaga, então ambos
        // podem existir mesmo com o plano de uma vaga.
        await CreateProfessionalAsync("Sem serviço A");
        await CreateProfessionalAsync("Sem serviço B");

        // O primeiro vínculo consome a única vaga.
        var service = await CreateServiceAsync("Serviço A");
        var first = await CreateProfessionalAsync("Com serviço A");

        var link = await LinkAsync(first, service);

        Assert.Equal(HttpStatusCode.NoContent, link.StatusCode);
    }

    [Fact]
    public async Task Associar_Servico_Acima_Do_Limite_Eh_Bloqueado()
    {
        // Ocupa a única vaga.
        var firstService = await CreateServiceAsync("Serviço 1");
        var firstProfessional = await CreateProfessionalAsync("Profissional 1");

        Assert.Equal(HttpStatusCode.NoContent, (await LinkAsync(firstProfessional, firstService)).StatusCode);

        // Um segundo profissional sem serviço não consome vaga...
        var secondProfessional = await CreateProfessionalAsync("Profissional 2");
        var secondService = await CreateServiceAsync("Serviço 2");

        // ...mas associá-lo o tornaria elegível, ultrapassando o limite.
        var blocked = await LinkAsync(secondProfessional, secondService);

        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);

        var body = await blocked.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(body);
        Assert.Contains("plano", body!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Associar_Segundo_Servico_A_Quem_Ja_Ocupa_Vaga_Nao_Consome_Outra()
    {
        var firstService = await CreateServiceAsync("Serviço 1");
        var secondService = await CreateServiceAsync("Serviço 2");
        var professional = await CreateProfessionalAsync("Profissional");

        Assert.Equal(HttpStatusCode.NoContent, (await LinkAsync(professional, firstService)).StatusCode);

        // Já ocupa vaga: o segundo vínculo não acrescenta consumo.
        var second = await LinkAsync(professional, secondService);

        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
    }

    [Fact]
    public async Task Vinculo_Repetido_Eh_Idempotente()
    {
        var service = await CreateServiceAsync("Serviço");
        var professional = await CreateProfessionalAsync("Profissional");

        Assert.Equal(HttpStatusCode.NoContent, (await LinkAsync(professional, service)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await LinkAsync(professional, service)).StatusCode);
    }

    [Fact]
    public async Task Associacoes_Concorrentes_Pela_API_Respeitam_Limite_E_Nao_Retornam_500()
    {
        var service = await CreateServiceAsync("Serviço concorrente");
        var first = await CreateProfessionalAsync("Pessoa A");
        var second = await CreateProfessionalAsync("Pessoa B");
        var results = await Task.WhenAll(LinkAsync(first, service), LinkAsync(second, service));
        Assert.Single(results, response => response.StatusCode == HttpStatusCode.NoContent);
        Assert.All(results, response => Assert.Contains(response.StatusCode,
            new[] { HttpStatusCode.NoContent, HttpStatusCode.BadRequest, HttpStatusCode.Conflict }));
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(TenantId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.ProfessionalServices.CountAsync());
    }

    // ---------- IsServiceProvider não consome vaga ----------

    [Fact]
    public async Task Alterar_Atuacao_Com_Plano_Cheio_Nao_Eh_Bloqueado()
    {
        // Ocupa a única vaga.
        var service = await CreateServiceAsync("Serviço");
        var occupying = await CreateProfessionalAsync("Ocupante");

        Assert.Equal(HttpStatusCode.NoContent, (await LinkAsync(occupying, service)).StatusCode);

        // Um segundo profissional sem serviço não ocupa vaga.
        var other = await CreateProfessionalAsync("Sem serviço");

        await SendAsync(HttpMethod.Patch,
            $"/api/v1/team/professionals/{other}/service-provider",
            new { isServiceProvider = false });

        // Alternar a atuação não torna ninguém elegível: não há limite a
        // verificar, e a operação deve ser aceita mesmo com o plano cheio.
        var response = await SendAsync(
            HttpMethod.Patch,
            $"/api/v1/team/professionals/{other}/service-provider",
            new { isServiceProvider = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Atuacao_Nao_Substitui_O_Criterio_De_Vaga()
    {
        // Profissional com atuação desligada, mas com serviço ativo vinculado:
        // ocupa vaga, porque o critério é ativo + serviço ativo + jornada.
        var service = await CreateServiceAsync("Serviço");
        var professional = await CreateProfessionalAsync("Profissional");

        await SendAsync(
            HttpMethod.Patch,
            $"/api/v1/team/professionals/{professional}/service-provider",
            new { isServiceProvider = false });

        Assert.Equal(HttpStatusCode.NoContent, (await LinkAsync(professional, service)).StatusCode);

        // A vaga está ocupada: um segundo profissional com serviço é bloqueado.
        var secondService = await CreateServiceAsync("Serviço 2");
        var second = await CreateProfessionalAsync("Profissional 2");

        var blocked = await LinkAsync(second, secondService);

        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
    }
}
