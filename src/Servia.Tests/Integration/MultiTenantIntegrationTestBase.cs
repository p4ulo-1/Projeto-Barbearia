using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Servia.Api.Contracts;
using Servia.Tests.Api;
using Servia.Domain.Identity;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Identity;
using Servia.Infrastructure.Persistence;

namespace Servia.Tests.Integration;

/// <summary>
/// Base para os testes de integração multi-tenant (Etapa 8).
///
/// Cada classe de teste sobe um container PostgreSQL 17 próprio e cria
/// dois estabelecimentos independentes. Os testes exercitam a API real:
/// autenticação, resolução de tenant, filtro global e constraints do banco.
///
/// <b>Por que PostgreSQL:</b> é o banco de referência do ambiente real.
/// Testar em outro provider mascararia violações de FK, diferenças de
/// tradução de consultas e semântica de texto.
/// </summary>
public abstract class MultiTenantIntegrationTestBase : IClassFixture<IsolatedApiFactory>
{
    protected const string Password = "SenhaSegura123";

    protected static readonly Guid TenantA = Guid.NewGuid();
    protected static readonly Guid TenantB = Guid.NewGuid();

    private readonly IsolatedApiFactory _factory;

    protected MultiTenantIntegrationTestBase(IsolatedApiFactory factory)
    {
        _factory = factory;
        Client = factory.CreateClient();

        SeedTenants();
    }

    protected HttpClient Client { get; }

    protected IServiceProvider Services => _factory.Services;

    /// <summary>
    /// Cria um proprietário em cada tenant. O seed roda uma única vez por
    /// container: o xUnit cria uma instância da classe por teste.
    /// </summary>
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

    /// <summary>Autentica no tenant informado e devolve o token de acesso.</summary>
    protected async Task<string> SignInAsync(Guid tenantId, string email)
    {
        var response = await Client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(
            tenantId, email, Password));

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();

        return body!.AccessToken;
    }

    /// <summary>Autentica no tenant A.</summary>
    protected Task<string> SignInAsTenantAAsync() => SignInAsync(TenantA, "dono-a@exemplo.com");

    /// <summary>Autentica no tenant B.</summary>
    protected Task<string> SignInAsTenantBAsync() => SignInAsync(TenantB, "dono-b@exemplo.com");

    /// <summary>Executa uma requisição autenticada.</summary>
    protected async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string url,
        string token,
        object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await Client.SendAsync(request);
    }

    /// <summary>Executa uma operação no banco com o tenant informado.</summary>
    protected async Task<T> WithTenantAsync<T>(Guid tenantId, Func<AppDbContext, Task<T>> operation)
    {
        using var scope = _factory.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await operation(context);
    }
}
