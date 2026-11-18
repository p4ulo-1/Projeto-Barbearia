using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Servia.Api.Contracts;
using Servia.Api.Controllers;
using Servia.Domain.Common;
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