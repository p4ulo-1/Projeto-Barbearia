using System.Net;
using System.Net.Http.Json;
using Servia.Api.Contracts;
using Servia.Api.Controllers;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Integrations;
using Servia.Tests.Common;
using Servia.Tests.Api;

namespace Servia.Tests.Integration;

/// <summary>
/// F21.1–F21.3 — cadastro público.
///
/// O visitante cria o próprio estabelecimento com trial de 14 dias, sem
/// intervenção da plataforma. É o destino do CTA "Começar agora".
/// </summary>
public class PublicSignupTests : IClassFixture<IsolatedApiFactory>
{
    private const string Password = "SenhaSegura123";

    private readonly HttpClient _client;

    public PublicSignupTests(IsolatedApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static SignupRequest NewRequest(string slug) =>
        new(slug, "Barbearia Nova", SegmentType.Barbershop, "12345678000190",
            "Ana Souza", $"ana-{Guid.NewGuid():N}@exemplo.com", Password);

    // ---------- F21.1: cadastro ----------

    [Fact]
    public async Task Cadastro_Publico_Cria_Tenant_Com_Trial()
    {
        var slug = $"nova-{Guid.NewGuid():N}"[..20];

        var response = await _client.PostAsJsonAsync("/api/v1/public/signup", NewRequest(slug));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<SignupResponse>();

        Assert.NotNull(body);
        Assert.Equal(slug.ToLowerInvariant(), body!.Slug);
        Assert.Equal("Trialing", body.SubscriptionStatus);
        Assert.NotNull(body.TrialEndsAt);
    }

    [Fact]
    public async Task Cadastro_Publico_Nao_Exige_Autenticacao()
    {
        var slug = $"aberto-{Guid.NewGuid():N}"[..20];

        // Sem header de autorizacao: e um endpoint publico.
        var response = await _client.PostAsJsonAsync("/api/v1/public/signup", NewRequest(slug));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Proprietario_Criado_Consegue_Autenticar()
    {
        var slug = $"login-{Guid.NewGuid():N}"[..20];
        var email = $"dono-{Guid.NewGuid():N}@exemplo.com";

        var request = new SignupRequest(
            slug, "Barbearia Login", SegmentType.Barbershop, "12345678000190",
            "Dono", email, Password);

        var created = await _client.PostAsJsonAsync("/api/v1/public/signup", request);
        var body = await created.Content.ReadFromJsonAsync<SignupResponse>();

        var signIn = await _client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(
            body!.TenantId, email, Password));

        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
    }

    // ---------- F21.2: unicidade ----------

    [Fact]
    public async Task Slug_Duplicado_Retorna_409()
    {
        var slug = $"dup-{Guid.NewGuid():N}"[..20];

        await _client.PostAsJsonAsync("/api/v1/public/signup", NewRequest(slug));
        var second = await _client.PostAsJsonAsync("/api/v1/public/signup", NewRequest(slug));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Slug_Invalido_Retorna_400()
    {
        var request = NewRequest("Slug Invalido!") with { Slug = "Slug Invalido!" };

        var response = await _client.PostAsJsonAsync("/api/v1/public/signup", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Senha_Curta_Retorna_400()
    {
        var request = NewRequest($"curta-{Guid.NewGuid():N}"[..20]) with { OwnerPassword = "123" };

        var response = await _client.PostAsJsonAsync("/api/v1/public/signup", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- F21.3: proteção contra abuso ----------

    [Fact]
    public void Throttle_Bloqueia_Apos_O_Limite()
    {
        // Testado diretamente: em Development o limite e desabilitado
        // para nao bloquear os testes de integracao, que vem do mesmo IP.
        var throttle = new InMemorySignupThrottle(new FixedTimeProvider(DateTimeOffset.UtcNow));

        for (var index = 0; index < InMemorySignupThrottle.MaxAttempts; index++)
        {
            Assert.True(throttle.TryAcquire("192.0.2.1"));
        }

        Assert.False(throttle.TryAcquire("192.0.2.1"));
    }

    [Fact]
    public void Throttle_Isola_Origens_Diferentes()
    {
        var throttle = new InMemorySignupThrottle(new FixedTimeProvider(DateTimeOffset.UtcNow));

        for (var index = 0; index < InMemorySignupThrottle.MaxAttempts; index++)
        {
            throttle.TryAcquire("192.0.2.1");
        }

        // Outra origem nao e afetada pelo limite da primeira.
        Assert.True(throttle.TryAcquire("192.0.2.2"));
    }

    [Fact]
    public void Throttle_Libera_Apos_A_Janela()
    {
        var time = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var throttle = new InMemorySignupThrottle(time);

        for (var index = 0; index < InMemorySignupThrottle.MaxAttempts; index++)
        {
            throttle.TryAcquire("192.0.2.1");
        }

        Assert.False(throttle.TryAcquire("192.0.2.1"));

        time.Advance(InMemorySignupThrottle.Window + TimeSpan.FromMinutes(1));

        Assert.True(throttle.TryAcquire("192.0.2.1"));
    }
}

/// <summary>Provedor de tempo que pode avancar, para testar janelas.</summary>
internal sealed class MutableTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan delta) => _now = _now.Add(delta);
}
