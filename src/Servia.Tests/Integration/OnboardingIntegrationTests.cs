using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Servia.Api.Contracts;
using Servia.Domain.Ai;
using Servia.Domain.Identity;
using Servia.Domain.Sites;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;
using Servia.Tests.Api;

namespace Servia.Tests.Integration;

/// <summary>
/// Fluxo de onboarding assistido por IA contra a API real (itens 1–2).
///
/// Os provedores de IA são substituídos por dublês determinísticos: a
/// regra de negócio é o que se testa, não o fornecedor externo. O banco é
/// PostgreSQL 17 via Testcontainers, como no ambiente real.
/// </summary>
public sealed class OnboardingIntegrationTests : IClassFixture<OnboardingApiFactory>
{
    private readonly OnboardingApiFactory _factory;
    private readonly HttpClient _client;

    public OnboardingIntegrationTests(OnboardingApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureSeeded();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Fluxo_Completo_Inicia_Conversa_Gera_Aprova_E_Publica()
    {
        var auth = await AuthenticatedClientAsync();

        var start = await auth.PostAsJsonAsync(
            "/api/v1/onboarding/start",
            new { businessName = "Barbearia Teste", segment = 1 });

        Assert.Equal(HttpStatusCode.OK, start.StatusCode);

        var session = await start.Content.ReadFromJsonAsync<SessionDto>();
        Assert.NotNull(session);
        Assert.Equal("Collecting", session!.Status);

        // Conversa: a IA pede uma pergunta e extrai um fato.
        var message = await auth.PostAsJsonAsync(
            $"/api/v1/onboarding/sessions/{session.Id}/messages",
            new { message = "Somos uma barbearia no centro" });

        Assert.Equal(HttpStatusCode.OK, message.StatusCode);

        // Historico persistido e recuperavel.
        var history = await auth.GetFromJsonAsync<List<MessageDto>>(
            $"/api/v1/onboarding/sessions/{session.Id}/messages");
        Assert.NotNull(history);
        Assert.True(history!.Count >= 2);

        // Gerar, submeter, aprovar e publicar.
        Assert.Equal(HttpStatusCode.OK,
            (await auth.PostAsync($"/api/v1/onboarding/sessions/{session.Id}/generate", null)).StatusCode);

        Assert.Equal(HttpStatusCode.OK,
            (await auth.PostAsync($"/api/v1/onboarding/sessions/{session.Id}/submit", null)).StatusCode);

        Assert.Equal(HttpStatusCode.OK,
            (await auth.PostAsync($"/api/v1/onboarding/sessions/{session.Id}/approve", null)).StatusCode);

        var publish = await auth.PostAsJsonAsync(
            $"/api/v1/onboarding/sessions/{session.Id}/publish",
            new { subdomain = $"barbearia-{Guid.NewGuid():N}"[..20] });

        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
    }

    [Fact]
    public async Task Publicar_Sem_Aprovar_E_Rejeitado()
    {
        var auth = await AuthenticatedClientAsync();

        var start = await auth.PostAsJsonAsync(
            "/api/v1/onboarding/start",
            new { businessName = "Salão Sem Aprovação", segment = 2 });

        var session = await start.Content.ReadFromJsonAsync<SessionDto>();

        await auth.PostAsJsonAsync(
            $"/api/v1/onboarding/sessions/{session!.Id}/messages",
            new { message = "Salão de beleza" });

        await auth.PostAsync($"/api/v1/onboarding/sessions/{session.Id}/generate", null);
        await auth.PostAsync($"/api/v1/onboarding/sessions/{session.Id}/submit", null);

        var publish = await auth.PostAsJsonAsync(
            $"/api/v1/onboarding/sessions/{session.Id}/publish",
            new { subdomain = "salao-sem-aprovacao" });

        Assert.Equal(HttpStatusCode.BadRequest, publish.StatusCode);
    }

    [Fact]
    public async Task Iniciar_Novamente_Retoma_A_Sessao_Aberta()
    {
        var auth = await AuthenticatedClientAsync();

        var first = await auth.PostAsJsonAsync(
            "/api/v1/onboarding/start",
            new { businessName = "Estúdio Retomada", segment = 4 });

        var firstSession = await first.Content.ReadFromJsonAsync<SessionDto>();

        var second = await auth.PostAsJsonAsync(
            "/api/v1/onboarding/start",
            new { businessName = "Outro nome", segment = 4 });

        var secondSession = await second.Content.ReadFromJsonAsync<SessionDto>();

        Assert.Equal(firstSession!.Id, secondSession!.Id);
    }

    [Fact]
    public async Task Requisicao_Sem_Token_E_Negada()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/onboarding/start",
            new { businessName = "Sem Token", segment = 1 });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Download_De_Arquivo_Usa_Content_Disposition_Seguro()
    {
        var auth = await AuthenticatedClientAsync();

        var start = await auth.PostAsJsonAsync(
            "/api/v1/onboarding/start",
            new { businessName = "Barbearia Arquivo", segment = 1 });

        var session = await start.Content.ReadFromJsonAsync<SessionDto>();

        // PNG minimo valido: assinatura + chunk IHDR (largura/altura 1x1).
        // C4 exige decodificacao real, nao apenas a assinatura.
        var png = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D,
            0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01,
            0x00, 0x00, 0x00, 0x01,
            0x08, 0x06, 0x00, 0x00, 0x00
        };

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(png);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        form.Add(fileContent, "file", "logo.png");
        form.Add(new StringContent("1"), "kind"); // SiteAssetKind.Logo

        var upload = await auth.PostAsync(
            $"/api/v1/onboarding/sessions/{session!.Id}/assets", form);

        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);

        var asset = await upload.Content.ReadFromJsonAsync<AssetDto>();

        var download = await auth.GetAsync(
            $"/api/v1/onboarding/sessions/{session.Id}/assets/{asset!.Id}/content");

        Assert.Equal(HttpStatusCode.OK, download.StatusCode);

        var disposition = download.Content.Headers.ContentDisposition;

        // Servido como anexo e sem quebra de linha no cabecalho.
        Assert.Equal("attachment", disposition?.DispositionType);
        Assert.DoesNotContain("\r", disposition?.ToString() ?? string.Empty);
        Assert.DoesNotContain("\n", disposition?.ToString() ?? string.Empty);
    }

    private async Task<HttpClient> AuthenticatedClientAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(
            OnboardingApiFactory.TenantId, OnboardingApiFactory.OwnerEmail, OnboardingApiFactory.Password));

        var body = await response.Content.ReadFromJsonAsync<Servia.Api.Contracts.AuthResponse>();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);

        return client;
    }

    private sealed record SessionDto(Guid Id, string Status);

    private sealed record AssetDto(Guid Id, string FileName);

    private sealed record MessageDto(string Role, string Content);
}

/// <summary>
/// Fábrica que substitui os provedores de IA por dublês determinísticos e
/// prepara um estabelecimento com proprietário autenticável.
/// </summary>
public sealed class OnboardingApiFactory : IsolatedApiFactory
{
    public const string Password = "SenhaSegura123";

    public static Guid TenantId { get; private set; } = Guid.NewGuid();

    public const string OwnerEmail = "dono-onboarding@exemplo.com";

    private bool _seeded;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            RemoveAll(services, typeof(IAiConversationProvider));
            RemoveAll(services, typeof(IAiSiteGenerator));

            services.AddScoped<IAiConversationProvider, FakeConversationProvider>();
            services.AddScoped<IAiSiteGenerator, FakeSiteGenerator>();
        });
    }

    /// <summary>
    /// Cria o estabelecimento e o proprietário antes de servir a primeira
    /// requisição. Executa uma única vez por container.
    /// </summary>
    public void EnsureSeeded()
    {
        if (_seeded)
        {
            return;
        }

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        if (!context.Users.IgnoreQueryFilters().Any(user => user.Email == OwnerEmail))
        {
            var now = DateTimeOffset.UtcNow;

            var tenant = Tenant.Create(
                "onboarding-teste", "Onboarding Teste", SegmentType.Barbershop, "12345678000190", now);

            context.Tenants.Add(tenant);

            // O usuario pertence ao tenant criado; o teste autentica com ele.
            TenantId = tenant.Id;

            context.Users.Add(User.CreateForTenant(
                tenant.Id, "Dono Onboarding", OwnerEmail, hasher.Hash(Password), Role.Owner, now));

            context.SaveChanges();
        }

        _seeded = true;
    }

    private static void RemoveAll(IServiceCollection services, Type serviceType)
    {
        foreach (var descriptor in services.Where(item => item.ServiceType == serviceType).ToList())
        {
            services.Remove(descriptor);
        }
    }
}

/// <summary>Conversa dublê: uma pergunta e depois conclusão.</summary>
internal sealed class FakeConversationProvider : IAiConversationProvider
{
    private int _turn;

    public string ProviderName => "fake";

    public Task<AiConversationTurn> ContinueAsync(
        AiConversationContext context,
        CancellationToken cancellationToken = default)
    {
        _turn++;

        var facts = new Dictionary<string, string> { ["description"] = "Estabelecimento de serviços" };

        return _turn >= 2
            ? Task.FromResult(AiConversationTurn.Completed(facts, 1))
            : Task.FromResult(AiConversationTurn.Asked(
                "Qual o endereço do estabelecimento?", facts, 1));
    }
}

/// <summary>Gerador dublê: proposta completa e válida.</summary>
internal sealed class FakeSiteGenerator : IAiSiteGenerator
{
    public string ProviderName => "fake";

    public Task<AiGenerationResult> GenerateAsync(
        AiGenerationRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(AiGenerationResult.Generated(Build(request), 2));

    public Task<AiGenerationResult> RefineAsync(
        AiGenerationRequest request,
        AiChangeSet current,
        string instruction,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(AiGenerationResult.Generated(Build(request), 2));

    private static AiChangeSet Build(AiGenerationRequest request)
    {
        var changeSet = AiChangeSet.Create(
            request.TenantId, $"Site para {request.BusinessName}", SiteTheme.Default);

        changeSet.AddSection(SiteComponentType.Hero,
            new Dictionary<string, string> { ["title"] = request.BusinessName });

        return changeSet;
    }
}
