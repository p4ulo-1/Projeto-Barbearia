using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Servia.Api.Contracts;
using Servia.Domain.Identity;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;
using Servia.Tests.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Servia.Tests.Integration;

/// <summary>
/// Onboarding sem provedor de IA configurado (item 1.1).
///
/// <b>Prova:</b> sem credencial, a aplicação sobe normalmente e a conversa
/// falha com motivo explícito (<c>400</c>), nunca com <c>500</c> nem com
/// conteúdo simulado. Uma integração falsa faria o produto parecer pronto.
/// </summary>
public sealed class OnboardingUnconfiguredAiTests : IClassFixture<IsolatedApiFactory>
{
    private readonly IsolatedApiFactory _factory;

    public OnboardingUnconfiguredAiTests(IsolatedApiFactory factory)
    {
        _factory = factory;
        EnsureSeeded(factory);
    }

    [Fact]
    public async Task Conversa_Sem_Provedor_Retorna_400_Com_Motivo()
    {
        var client = _factory.CreateClient();

        var signIn = await client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(
            TenantId, "dono-sem-ia@exemplo.com", "SenhaSegura123"));

        var auth = await signIn.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var start = await client.PostAsJsonAsync(
            "/api/v1/onboarding/start",
            new { businessName = "Barbearia Sem IA", segment = 1 });

        Assert.Equal(HttpStatusCode.OK, start.StatusCode);

        var session = await start.Content.ReadFromJsonAsync<SessionStart>();

        var message = await client.PostAsJsonAsync(
            $"/api/v1/onboarding/sessions/{session!.Id}/messages",
            new { message = "Somos uma barbearia" });

        // Nao configurado: erro de negocio, nao erro interno.
        Assert.Equal(HttpStatusCode.BadRequest, message.StatusCode);

        var error = await message.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("não está configurado", error!.Message);
    }

    private static readonly Guid TenantId = Guid.NewGuid();

    private static void EnsureSeeded(IsolatedApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        if (context.Users.IgnoreQueryFilters().Any(user => user.Email == "dono-sem-ia@exemplo.com"))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;

        context.Tenants.Add(Tenant.Create(
            "sem-ia-teste", "Sem IA", SegmentType.Barbershop, "33333333000133", now));

        context.Users.Add(User.CreateForTenant(
            TenantId, "Dono Sem IA", "dono-sem-ia@exemplo.com", hasher.Hash("SenhaSegura123"),
            Role.Owner, now));

        context.SaveChanges();
    }

    private sealed record SessionStart(Guid Id);
}
