using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Servia.Api.Contracts;
using Servia.Domain.Identity;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Identity;
using Servia.Infrastructure.Persistence;

namespace Servia.Tests.Api;

/// <summary>
/// F10.3 — middleware de autenticação JWT.
/// F10.4 — autorização por permissão.
/// F10.5 — tenant resolvido pelo token.
/// F10.8 — testes de autenticação e autorização.
///
/// Exercita o pipeline real: autenticação, autorização e resolução de tenant.
/// </summary>
public class AuthorizationTests : IClassFixture<IsolatedApiFactory>
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private const string Password = "SenhaSegura123";

    private readonly IsolatedApiFactory _factory;
    private readonly HttpClient _client;

    public AuthorizationTests(IsolatedApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();

        SeedUsers();
    }

    /// <summary>
    /// Cria os usuarios de teste: um proprietario e um cliente.
    /// O proprietario tem ManageAppointments; o cliente nao.
    /// </summary>
    private void SeedUsers()
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(TenantId);

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        if (context.Users.Any())
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;

        context.Users.Add(User.CreateForTenant(
            TenantId, "Proprietario", "owner@exemplo.com",
            hasher.Hash(Password), Role.Owner, now));

        context.Users.Add(User.CreateForTenant(
            TenantId, "Cliente", "cliente@exemplo.com",
            hasher.Hash(Password), Role.Customer, now));

        context.SaveChanges();
    }

    // ---------- F10.3: autenticacao ----------

    [Fact]
    public async Task Rota_Protegida_Sem_Token_Retorna_401()
    {
        var response = await _client.PostAsJsonAsync("/api/appointments", new { });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Rota_Protegida_Com_Token_Invalido_Retorna_401()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/appointments")
        {
            Content = JsonContent.Create(new { })
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "token-invalido");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_Com_Credenciais_Validas_Retorna_Tokens()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(
            TenantId, "owner@exemplo.com", Password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(body.RefreshToken));
    }

    [Fact]
    public async Task Login_Com_Senha_Errada_Retorna_401()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(
            TenantId, "owner@exemplo.com", "SenhaErrada123"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- F10.4: autorizacao por permissao ----------

    [Fact]
    public async Task Rota_Protegida_Com_Token_Valido_Nao_Retorna_401()
    {
        var token = await SignInAsync("owner@exemplo.com");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/appointments")
        {
            Content = JsonContent.Create(new { })
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        // Pode ser 400 por corpo invalido, mas nao 401: o token foi aceito.
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Cliente_Sem_Permissao_Recebe_403()
    {
        // O papel Customer nao possui ManageAppointments.
        var token = await SignInAsync("cliente@exemplo.com");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/appointments")
        {
            Content = JsonContent.Create(new { })
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------- F10.5: tenant pelo token ----------

    [Fact]
    public async Task Token_Carrega_O_Tenant_Do_Usuario()
    {
        var token = await SignInAsync("owner@exemplo.com");

        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var parsed = handler.ReadJwtToken(token);

        var tenantClaim = parsed.Claims
            .FirstOrDefault(claim => claim.Type == JwtTokenIssuer.TenantIdClaim);

        Assert.NotNull(tenantClaim);
        Assert.Equal(TenantId.ToString(), tenantClaim!.Value);
    }

    [Fact]
    public async Task Token_Carrega_O_Papel_Do_Usuario()
    {
        var token = await SignInAsync("owner@exemplo.com");

        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var parsed = handler.ReadJwtToken(token);

        var roleClaim = parsed.Claims
            .FirstOrDefault(claim => claim.Type == JwtTokenIssuer.RoleClaim);

        Assert.NotNull(roleClaim);
        Assert.Equal(nameof(Role.Owner), roleClaim!.Value);
    }

    // ---------- F10.6, F10.7: renovacao e logout ----------

    [Fact]
    public async Task Renovar_Sessao_Retorna_Novos_Tokens()
    {
        var signIn = await SignInFullAsync("owner@exemplo.com");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh")
        {
            Content = JsonContent.Create(new RefreshRequest(signIn.RefreshToken))
        };

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotEqual(signIn.RefreshToken, body!.RefreshToken);
    }

    [Fact]
    public async Task Logout_Revoga_O_Token()
    {
        var signIn = await SignInFullAsync("owner@exemplo.com");

        using var signOut = new HttpRequestMessage(HttpMethod.Post, "/api/auth/sign-out")
        {
            Content = JsonContent.Create(new SignOutRequest(signIn.RefreshToken))
        };

        var signOutResponse = await _client.SendAsync(signOut);
        Assert.Equal(HttpStatusCode.NoContent, signOutResponse.StatusCode);

        // O token revogado nao renova mais.
        using var refresh = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh")
        {
            Content = JsonContent.Create(new RefreshRequest(signIn.RefreshToken))
        };

        var refreshResponse = await _client.SendAsync(refresh);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    // ---------- Auxiliares ----------

    private async Task<string> SignInAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(
            TenantId, email, Password));

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();

        return body!.AccessToken;
    }

    private async Task<AuthResponse> SignInFullAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(
            TenantId, email, Password));

        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

}
