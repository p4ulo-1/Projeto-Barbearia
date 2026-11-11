using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
/// Etapa 3 da auditoria — resolução de tenant e acesso administrativo.
///
/// Antes da correção, o middleware aceitava o header <c>X-Tenant-Id</c>
/// como fallback. Em Development, qualquer cliente escolhia o
/// estabelecimento sem sequer autenticar.
///
/// Estes testes provam que o tenant vem exclusivamente do token.
/// </summary>
public class TenantResolutionTests : IClassFixture<IsolatedApiFactory>
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private const string Password = "SenhaSegura123";

    private readonly IsolatedApiFactory _factory;
    private readonly HttpClient _client;

    public TenantResolutionTests(IsolatedApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();

        SeedTenants();
    }

    private void SeedTenants()
    {
        using var scope = _factory.Services.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        if (context.Users.IgnoreQueryFilters().Any())
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;

        context.Users.Add(User.CreateForTenant(
            TenantA, "Dono A", "dono-a@exemplo.com", hasher.Hash(Password), Role.Owner, now));

        context.Users.Add(User.CreateForTenant(
            TenantB, "Dono B", "dono-b@exemplo.com", hasher.Hash(Password), Role.Owner, now));

        context.SaveChanges();
    }

    private async Task<string> SignInAsync(Guid tenantId, string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(
            tenantId, email, Password));

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();

        return body!.AccessToken;
    }

    // ---------- Header nao e aceito ----------

    [Fact]
    public async Task Header_De_Tenant_Nao_Eh_Aceito_Sem_Token()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/catalog/services");
        request.Headers.Add("X-Tenant-Id", TenantA.ToString());

        var response = await _client.SendAsync(request);

        // Sem token, o header nao resolve nada: a rota exige autenticacao.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Header_De_Outro_Tenant_Nao_Muda_O_Tenant_Efetivo()
    {
        var token = await SignInAsync(TenantA, "dono-a@exemplo.com");

        // Cria um servico no tenant A.
        using var create = new HttpRequestMessage(HttpMethod.Post, "/api/v1/catalog/services")
        {
            Content = JsonContent.Create(new CreateServiceRequest("Corte A", 50m, 30))
        };

        create.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await _client.SendAsync(create);

        // Tenta listar como se fosse o tenant B, forjando o header.
        using var list = new HttpRequestMessage(HttpMethod.Get, "/api/v1/catalog/services?page=1&pageSize=10");
        list.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        list.Headers.Add("X-Tenant-Id", TenantB.ToString());

        var response = await _client.SendAsync(list);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedResponse<ServiceResponse>>();

        // O servico criado em A aparece: o header foi ignorado.
        Assert.Contains(page!.Items, item => item.Name == "Corte A");
    }

    [Fact]
    public async Task Token_De_A_Nao_Ve_Dados_De_B()
    {
        var tokenA = await SignInAsync(TenantA, "dono-a@exemplo.com");
        var tokenB = await SignInAsync(TenantB, "dono-b@exemplo.com");

        // Cria um servico exclusivo do tenant B.
        using var create = new HttpRequestMessage(HttpMethod.Post, "/api/v1/catalog/services")
        {
            Content = JsonContent.Create(new CreateServiceRequest("Corte B", 70m, 45))
        };

        create.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        await _client.SendAsync(create);

        // O tenant A lista seus servicos.
        using var list = new HttpRequestMessage(HttpMethod.Get, "/api/v1/catalog/services?page=1&pageSize=50");
        list.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

        var response = await _client.SendAsync(list);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<ServiceResponse>>();

        // O servico de B nao aparece para A.
        Assert.DoesNotContain(page!.Items, item => item.Name == "Corte B");
    }

    // ---------- Claims invalidas ----------

    [Fact]
    public async Task Header_Forjado_Nao_Cria_Tenant_Inexistente()
    {
        var token = await SignInAsync(TenantA, "dono-a@exemplo.com");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/catalog/services?page=1&pageSize=10");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-Tenant-Id", Guid.NewGuid().ToString());

        var response = await _client.SendAsync(request);

        // O header forjado nao cria um tenant: a resposta usa o tenant do token.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Rota_Protegida_Sem_Token_Retorna_401()
    {
        var response = await _client.GetAsync("/api/v1/catalog/services");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- Refresh sem header ----------

    [Fact]
    public async Task Renovacao_Resolve_Tenant_Pelo_Refresh_Token()
    {
        var signIn = await _client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(
            TenantA, "dono-a@exemplo.com", Password));

        var tokens = await signIn.Content.ReadFromJsonAsync<AuthResponse>();

        // Sem header: o tenant vem do proprio refresh token.
        var refresh = await _client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(tokens!.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);

        var renewed = await refresh.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotEqual(tokens.RefreshToken, renewed!.RefreshToken);
    }

    [Fact]
    public async Task Logout_Resolve_Tenant_Pelo_Refresh_Token()
    {
        var signIn = await _client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(
            TenantA, "dono-a@exemplo.com", Password));

        var tokens = await signIn.Content.ReadFromJsonAsync<AuthResponse>();

        var signOut = await _client.PostAsJsonAsync("/api/auth/sign-out",
            new SignOutRequest(tokens!.RefreshToken));

        Assert.Equal(HttpStatusCode.NoContent, signOut.StatusCode);

        // O token revogado nao renova mais.
        var refresh = await _client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(tokens.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }
}
