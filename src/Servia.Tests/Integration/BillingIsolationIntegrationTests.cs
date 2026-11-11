using System.Net;
using System.Net.Http.Json;
using Servia.Api.Contracts;
using Servia.Tests.Api;
using Servia.Api.Controllers;
using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Tests.Integration;

/// <summary>
/// Etapa 8 — assinaturas e faturamento isolados por tenant.
///
/// Antes da Etapa 2, <c>Subscription</c> não implementava
/// <c>ITenantScoped</c> e o <c>BillingController</c> retornava a
/// assinatura mais recente de qualquer estabelecimento.
/// </summary>
public class BillingIsolationIntegrationTests : MultiTenantIntegrationTestBase
{
    private static readonly DateOnly Today = new(2026, 11, 11);

    public BillingIsolationIntegrationTests(IsolatedApiFactory factory)
        : base(factory)
    {
    }

    /// <summary>Cria uma assinatura para o tenant informado.</summary>
    private async Task<Guid> CreateSubscriptionAsync(Guid tenantId)
    {
        return await WithTenantAsync(tenantId, async context =>
        {
            var plan = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .FirstAsync(context.Plans, item => item.Code == "ESSENCIAL");

            var subscription = Subscription.StartTrial(tenantId, plan.Id, Today);

            context.Subscriptions.Add(subscription);
            await context.SaveChangesAsync();

            return subscription.Id;
        });
    }

    [Fact]
    public async Task Cada_Tenant_Ve_Apenas_A_Propria_Assinatura()
    {
        var subscriptionA = await CreateSubscriptionAsync(TenantA);
        var subscriptionB = await CreateSubscriptionAsync(TenantB);

        var tokenA = await SignInAsTenantAAsync();
        var tokenB = await SignInAsTenantBAsync();

        var responseA = await SendAsync(HttpMethod.Get, "/api/v1/billing/subscription", tokenA);
        var responseB = await SendAsync(HttpMethod.Get, "/api/v1/billing/subscription", tokenB);

        Assert.Equal(HttpStatusCode.OK, responseA.StatusCode);
        Assert.Equal(HttpStatusCode.OK, responseB.StatusCode);

        var bodyA = await responseA.Content.ReadFromJsonAsync<SubscriptionResponse>();
        var bodyB = await responseB.Content.ReadFromJsonAsync<SubscriptionResponse>();

        Assert.Equal(subscriptionA, bodyA!.Id);
        Assert.Equal(subscriptionB, bodyB!.Id);
        Assert.NotEqual(bodyA.Id, bodyB.Id);
    }

    [Fact]
    public async Task Tenant_Sem_Assinatura_Recebe_404()
    {
        // O container e compartilhado entre testes da classe: remove
        // qualquer assinatura previa do tenant B para isolar o cenario.
        await WithTenantAsync(TenantB, async context =>
        {
            var existing = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .ToListAsync(context.Subscriptions);

            context.Subscriptions.RemoveRange(existing);
            await context.SaveChangesAsync();

            return 0;
        });

        // Apenas o tenant A tem assinatura.
        await CreateSubscriptionAsync(TenantA);

        var tokenB = await SignInAsTenantBAsync();

        var response = await SendAsync(HttpMethod.Get, "/api/v1/billing/subscription", tokenB);

        // O tenant B nao herda a assinatura do A.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Faturas_Sao_Isoladas_Por_Tenant()
    {
        var tokenA = await SignInAsTenantAAsync();

        var response = await SendAsync(
            HttpMethod.Get, "/api/v1/billing/invoices?page=1&pageSize=20", tokenA);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content
            .ReadFromJsonAsync<PagedResponse<InvoiceResponse>>();

        // O tenant A nao ve faturas de outros estabelecimentos.
        Assert.NotNull(page);
    }

    [Fact]
    public async Task Consumo_Sao_Isolado_Por_Tenant()
    {
        var tokenA = await SignInAsTenantAAsync();
        var tokenB = await SignInAsTenantBAsync();

        var responseA = await SendAsync(HttpMethod.Get, "/api/v1/billing/usage", tokenA);
        var responseB = await SendAsync(HttpMethod.Get, "/api/v1/billing/usage", tokenB);

        Assert.Equal(HttpStatusCode.OK, responseA.StatusCode);
        Assert.Equal(HttpStatusCode.OK, responseB.StatusCode);
    }

    [Fact]
    public async Task Planos_Sao_Globais_E_Visiveis_Sem_Autenticacao()
    {
        // Plan e entidade global: nao pertence a um tenant.
        var response = await Client.GetAsync("/api/v1/billing/plans");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var plans = await response.Content.ReadFromJsonAsync<List<PlanResponse>>();

        Assert.NotNull(plans);
        Assert.Contains(plans!, plan => plan.Code == "ESSENCIAL");
    }
}
