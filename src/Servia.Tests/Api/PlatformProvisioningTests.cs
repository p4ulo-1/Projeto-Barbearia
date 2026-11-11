using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Servia.Api.Contracts;
using Servia.Api.Controllers;
using Servia.Domain.Identity;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Identity;
using Servia.Infrastructure.Persistence;

namespace Servia.Tests.Api;

/// <summary>
/// F15.1 — provisionamento do primeiro tenant real.
/// F15.2 — configuração de produção.
///
/// Verifica que um estabelecimento é criado do zero pela API, com
/// assinatura em trial e proprietário, e que só um administrador da
/// plataforma pode fazer isso.
/// </summary>
public class PlatformProvisioningTests : IClassFixture<IsolatedApiFactory>
{
    private static readonly Guid PlatformTenantId = Guid.NewGuid();
    private const string Password = "SenhaSegura123";

    private readonly IsolatedApiFactory _factory;
    private readonly HttpClient _client;

    public PlatformProvisioningTests(IsolatedApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();

        SeedPlatformAdmin();
    }

    /// <summary>
    /// Cria o administrador da plataforma e os planos.
    /// O provisionamento exige um plano existente.
    /// </summary>
    private void SeedPlatformAdmin()
    {
        using var scope = _factory.Services.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        // Planos sao globais: nao dependem de tenant.
        if (!context.Plans.Any())
        {
            PlanSeeder.SeedAsync(context).GetAwaiter().GetResult();
        }

        // O administrador da plataforma tem TenantId vazio: a consulta
        // precisa ignorar o filtro global.
        if (context.Users.IgnoreQueryFilters().Any(user => user.Role == Role.PlatformAdmin))
        {
            return;
        }

        context.Users.Add(User.CreatePlatformAdmin(
            "Suporte", "admin@servia.app", hasher.Hash(Password), DateTimeOffset.UtcNow));

        context.SaveChanges();
    }

    private async Task<string> SignInAsPlatformAdminAsync()
    {
        // Administrador da plataforma nao pertence a um tenant (F1.13).
        var response = await _client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(
            Guid.Empty, "admin@servia.app", Password));

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();

        return body!.AccessToken;
    }

    private async Task<HttpResponseMessage> ProvisionAsync(
        string token,
        ProvisionTenantApiRequest request)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/platform/tenants")
        {
            Content = JsonContent.Create(request)
        };

        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await _client.SendAsync(message);
    }

    private static ProvisionTenantApiRequest NewRequest(string slug = "barbearia-real") =>
        new(slug, "Barbearia Real", SegmentType.Barbershop, "12.345.678/0001-90",
            "ESSENCIAL", "Ana Souza", "ana@barbeariareal.com", Password);

    // ---------- F15.1: provisionamento ----------

    [Fact]
    public async Task Administrador_Da_Plataforma_Provisiona_Tenant()
    {
        var token = await SignInAsPlatformAdminAsync();

        var response = await ProvisionAsync(token, NewRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ProvisionedTenantResponse>();

        Assert.NotNull(body);
        Assert.Equal("barbearia-real", body!.Slug);
        Assert.Equal("Trial", body.TenantStatus);
        Assert.Equal("Trialing", body.SubscriptionStatus);
        Assert.NotNull(body.TrialEndsAt);
    }

    [Fact]
    public async Task Tenant_Provisionado_Pode_Autenticar_Com_O_Proprietario()
    {
        var adminToken = await SignInAsPlatformAdminAsync();
        var slug = $"barbearia-{Guid.NewGuid():N}"[..20];

        var created = await ProvisionAsync(adminToken, NewRequest(slug));
        var provisioned = await created.Content.ReadFromJsonAsync<ProvisionedTenantResponse>();

        // O proprietario recem-criado consegue entrar no proprio tenant.
        var signIn = await _client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(
            provisioned!.TenantId, "ana@barbeariareal.com", Password));

        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
    }

    [Fact]
    public async Task Provisionamento_Rejeita_Slug_Duplicado()
    {
        var token = await SignInAsPlatformAdminAsync();
        var slug = $"dup-{Guid.NewGuid():N}"[..20];

        await ProvisionAsync(token, NewRequest(slug));
        var second = await ProvisionAsync(token, NewRequest(slug));

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Provisionamento_Rejeita_Plano_Inexistente()
    {
        var token = await SignInAsPlatformAdminAsync();

        var request = NewRequest($"plano-{Guid.NewGuid():N}"[..20]) with { PlanCode = "INEXISTENTE" };

        var response = await ProvisionAsync(token, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Provisionamento_Rejeita_Slug_Invalido()
    {
        var token = await SignInAsPlatformAdminAsync();

        var request = NewRequest("Slug Invalido!") with { Slug = "Slug Invalido!" };

        var response = await ProvisionAsync(token, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- F15.2: separacao de privilegios ----------

    [Fact]
    public async Task Usuario_De_Tenant_Nao_Pode_Provisionar()
    {
        // Cria um tenant e entra como o proprietario dele.
        var adminToken = await SignInAsPlatformAdminAsync();
        var slug = $"owner-{Guid.NewGuid():N}"[..20];

        var created = await ProvisionAsync(adminToken, NewRequest(slug));
        var provisioned = await created.Content.ReadFromJsonAsync<ProvisionedTenantResponse>();

        var signIn = await _client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(
            provisioned!.TenantId, "ana@barbeariareal.com", Password));

        var ownerTokens = await signIn.Content.ReadFromJsonAsync<AuthResponse>();

        var response = await ProvisionAsync(ownerTokens!.AccessToken, NewRequest($"outro-{Guid.NewGuid():N}"[..20]));

        // Um proprietario de estabelecimento nao cria outro estabelecimento.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Provisionamento_Sem_Token_Retorna_401()
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/platform/tenants")
        {
            Content = JsonContent.Create(NewRequest())
        };

        var response = await _client.SendAsync(message);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
