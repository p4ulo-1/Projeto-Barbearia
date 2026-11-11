using Microsoft.EntityFrameworkCore;
using Servia.Domain.Common;
using Servia.Domain.Identity;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;
using Servia.Infrastructure.Provisioning;
using Servia.Infrastructure.Tenancy;
using Servia.Tests.Persistence;

using Servia.Tests.Common;

namespace Servia.Tests.Provisioning;

/// <summary>
/// F1.4 — provisionamento do primeiro tenant.
///
/// Verifica que um estabelecimento é criado do zero, com assinatura em trial
/// e proprietário, de forma atômica e isolada.
/// </summary>
public class TenantProvisionerTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly PostgresTestDatabase _database = new();

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();

        // Os planos sao criados pelo PlanSeeder na inicializacao da
        // aplicacao. Aqui o banco e criado por migrations, entao o seed
        // precisa rodar uma unica vez por container.
        await using var context = _database.CreateContext();

        if (!await context.Plans.AnyAsync())
        {
            await PlanSeeder.SeedAsync(context);
        }
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private static ProvisionTenantRequest Request(string slug = "barbearia-xyz") =>
        new(
            slug,
            "Barbearia XYZ",
            SegmentType.Barbershop,
            "12.345.678/0001-90",
            "ESSENCIAL",
            "Ana Souza",
            "ana@exemplo.com",
            "hash-da-senha");

    private TenantProvisioner NewProvisioner()
    {
        var context = _database.CreateContext(new TenantContext());
        return new TenantProvisioner(context, new FixedTimeProvider(Now));
    }

    // ---------- Provisionamento ----------

    [Fact]
    public async Task Provisionar_Cria_Tenant_Assinatura_E_Proprietario()
    {
        var result = await NewProvisioner().ProvisionAsync(Request());

        Assert.Equal("barbearia-xyz", result.Tenant.Slug);
        Assert.Equal(TenantStatus.Trial, result.Tenant.Status);
        Assert.Equal(SubscriptionStatus.Trialing, result.Subscription.Status);
        Assert.Equal(Role.Owner, result.Owner.Role);
        Assert.Equal(result.Tenant.Id, result.Owner.TenantId);
        Assert.Equal(result.Tenant.Id, result.Subscription.TenantId);
    }

    [Fact]
    public async Task Provisionar_Persiste_Os_Tres_Registros()
    {
        var result = await NewProvisioner().ProvisionAsync(Request());

        // O usuario e filtrado por tenant: define o tenant para consulta-lo.
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(result.Tenant.Id);
        await using var context = _database.CreateContext(tenantContext);

        Assert.NotNull(await context.Tenants.FirstOrDefaultAsync(item => item.Id == result.Tenant.Id));
        Assert.NotNull(await context.Subscriptions.FirstOrDefaultAsync(item => item.Id == result.Subscription.Id));
        Assert.NotNull(await context.Users.FirstOrDefaultAsync(item => item.Id == result.Owner.Id));
    }

    [Fact]
    public async Task Tenant_Provisionado_Nao_Herde_Dados_De_Outro()
    {
        var first = await NewProvisioner().ProvisionAsync(Request("barbearia-a"));
        var second = await NewProvisioner().ProvisionAsync(Request("barbearia-b"));

        Assert.NotEqual(first.Tenant.Id, second.Tenant.Id);
        Assert.NotEqual(first.Owner.Id, second.Owner.Id);

        // Cada tenant tem apenas o próprio usuário.
        await using var context = _database.CreateContext(new TenantContext());
        var usersOfFirst = await context.Users
            .IgnoreQueryFilters()
            .CountAsync(item => item.TenantId == first.Tenant.Id);

        Assert.Equal(1, usersOfFirst);
    }

    // ---------- Validacoes ----------

    [Fact]
    public async Task Provisionar_Com_Plano_Inexistente_Lanca_Excecao()
    {
        var request = Request() with { PlanCode = "INEXISTENTE" };

        await Assert.ThrowsAsync<DomainRuleException>(() =>
            NewProvisioner().ProvisionAsync(request));
    }

    [Fact]
    public async Task Provisionar_Com_Slug_Duplicado_Lanca_Excecao()
    {
        await NewProvisioner().ProvisionAsync(Request("barbearia-xyz"));

        await Assert.ThrowsAsync<DomainRuleException>(() =>
            NewProvisioner().ProvisionAsync(Request("barbearia-xyz")));
    }

    [Fact]
    public async Task Provisionar_Com_Slug_Invalido_Lanca_Excecao()
    {
        var request = Request() with { Slug = "slug invalido!" };

        await Assert.ThrowsAsync<DomainRuleException>(() =>
            NewProvisioner().ProvisionAsync(request));
    }

    [Fact]
    public async Task Provisionar_Com_Email_Invalido_Lanca_Excecao()
    {
        var request = Request() with { OwnerEmail = "sem-arroba" };

        await Assert.ThrowsAsync<DomainRuleException>(() =>
            NewProvisioner().ProvisionAsync(request));
    }

    // ---------- Atomicidade ----------

    [Fact]
    public async Task Falha_No_Provisionamento_Nao_Grava_Nada()
    {
        // Email invalido: falha na criacao do usuario, apos tenant e assinatura.
        var request = Request() with { OwnerEmail = "invalido" };

        await Assert.ThrowsAsync<DomainRuleException>(() =>
            NewProvisioner().ProvisionAsync(request));

        await using var context = _database.CreateContext(new TenantContext());

        // Nada foi persistido: a operacao e atomica.
        Assert.Empty(await context.Tenants.ToListAsync());
        Assert.Empty(await context.Subscriptions.ToListAsync());
    }
}

