using Microsoft.EntityFrameworkCore;
using Servia.Domain.Common;
using Servia.Domain.Customers;
using Servia.Domain.Loyalty;
using Servia.Domain.Memberships;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;
using Servia.Infrastructure.Tenancy;

namespace Servia.Tests.Persistence;

/// <summary>
/// Etapa 4 da auditoria — integridade referencial entre entidades e tenants.
///
/// Antes da correção, apenas <c>Appointment</c> tinha FK composta. As
/// demais entidades tinham colunas soltas: nada no banco impedia associar
/// um cliente do tenant A a uma assinatura do tenant B.
///
/// Estes testes provam que o PostgreSQL rejeita a associação, sem
/// depender de validação da aplicação.
/// </summary>
public class BusinessEntityForeignKeyTests : IAsyncLifetime
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 11);

    private readonly PostgresTestDatabase _database = new();

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();

        await using var context = _database.CreateContext(new TenantContext());
        await PlanSeeder.SeedAsync(context);
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private static ITenantContext ContextFor(Guid tenantId)
    {
        var context = new TenantContext();
        context.SetTenant(tenantId);
        return context;
    }

    private async Task<Customer> CreateCustomerAsync(Guid tenantId, string name)
    {
        await using var context = _database.CreateContext(ContextFor(tenantId));

        var customer = Customer.Create(tenantId, name, "21999998888", now: Now);
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        return customer;
    }

    private async Task<MembershipPlan> CreatePlanAsync(Guid tenantId)
    {
        await using var context = _database.CreateContext(ContextFor(tenantId));

        var plan = MembershipPlan.Create(tenantId, "Clube", Money.From(99m));
        context.MembershipPlans.Add(plan);
        await context.SaveChangesAsync();

        return plan;
    }

    // ---------- Relacionamentos legítimos ----------

    [Fact]
    public async Task Assinatura_Com_Cliente_Do_Mesmo_Tenant_Eh_Aceita()
    {
        var customer = await CreateCustomerAsync(TenantA, "Ana");
        var plan = await CreatePlanAsync(TenantA);

        await using var context = _database.CreateContext(ContextFor(TenantA));

        var membership = Membership.Subscribe(
            TenantA, customer.Id, plan.Id, Today, Today.AddDays(30));

        context.Memberships.Add(membership);
        await context.SaveChangesAsync();

        Assert.Equal(TenantA, membership.TenantId);
    }

    [Fact]
    public async Task Conta_De_Fidelidade_Com_Cliente_Do_Mesmo_Tenant_Eh_Aceita()
    {
        var customer = await CreateCustomerAsync(TenantA, "Bruno");

        await using var context = _database.CreateContext(ContextFor(TenantA));

        var account = LoyaltyAccount.Open(TenantA, customer.Id);
        context.LoyaltyAccounts.Add(account);
        await context.SaveChangesAsync();

        Assert.Equal(TenantA, account.TenantId);
    }

    // ---------- Rejeição cross-tenant ----------

    [Fact]
    public async Task Assinatura_Com_Cliente_De_Outro_Tenant_Eh_Rejeitada()
    {
        var customerB = await CreateCustomerAsync(TenantB, "Cliente B");
        var planA = await CreatePlanAsync(TenantA);

        await using var context = _database.CreateContext(ContextFor(TenantA));

        // Tenant A tenta associar um cliente do tenant B.
        var membership = Membership.Subscribe(
            TenantA, customerB.Id, planA.Id, Today, Today.AddDays(30));

        context.Memberships.Add(membership);

        // O PostgreSQL rejeita: a FK composta exige (TenantId, Id) do mesmo tenant.
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        Assert.Contains("23503", exception.InnerException?.Message ?? string.Empty);
    }

    [Fact]
    public async Task Assinatura_Com_Clube_De_Outro_Tenant_Eh_Rejeitada()
    {
        var customerA = await CreateCustomerAsync(TenantA, "Cliente A");
        var planB = await CreatePlanAsync(TenantB);

        await using var context = _database.CreateContext(ContextFor(TenantA));

        var membership = Membership.Subscribe(
            TenantA, customerA.Id, planB.Id, Today, Today.AddDays(30));

        context.Memberships.Add(membership);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Conta_De_Fidelidade_Com_Cliente_De_Outro_Tenant_Eh_Rejeitada()
    {
        var customerB = await CreateCustomerAsync(TenantB, "Cliente B");

        await using var context = _database.CreateContext(ContextFor(TenantA));

        var account = LoyaltyAccount.Open(TenantA, customerB.Id);
        context.LoyaltyAccounts.Add(account);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Pacote_Com_Cliente_De_Outro_Tenant_Eh_Rejeitado()
    {
        var customerB = await CreateCustomerAsync(TenantB, "Cliente B");

        await using var context = _database.CreateContext(ContextFor(TenantA));

        var package = ServicePackage.Create(
            TenantA, "Pacote", Money.From(400m), 10, 180);

        context.ServicePackages.Add(package);
        await context.SaveChangesAsync();

        var usage = PackageUsage.Purchase(TenantA, customerB.Id, package, Today);
        context.PackageUsages.Add(usage);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    // ---------- Entidades globais ----------

    [Fact]
    public async Task Assinatura_Referencia_Plano_Global()
    {
        await CreateCustomerAsync(TenantA, "Ana");

        await using var context = _database.CreateContext(ContextFor(TenantA));

        // Plan e global: nao tem TenantId, entao a FK e simples.
        var plan = await context.Plans.FirstAsync(item => item.Code == "ESSENCIAL");

        var subscription = Subscription.StartTrial(TenantA, plan.Id, Today);
        context.Subscriptions.Add(subscription);
        await context.SaveChangesAsync();

        Assert.Equal(plan.Id, subscription.PlanId);
    }

    // ---------- Exclusão ----------

    [Fact]
    public async Task Exclusao_De_Cliente_Com_Assinatura_Eh_Bloqueada()
    {
        var customer = await CreateCustomerAsync(TenantA, "Ana");
        var plan = await CreatePlanAsync(TenantA);

        await using (var context = _database.CreateContext(ContextFor(TenantA)))
        {
            var membership = Membership.Subscribe(
                TenantA, customer.Id, plan.Id, Today, Today.AddDays(30));

            context.Memberships.Add(membership);
            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext(ContextFor(TenantA)))
        {
            var toDelete = await context.Customers.FirstAsync(item => item.Id == customer.Id);
            context.Customers.Remove(toDelete);

            // A FK com Restrict impede excluir um cliente com assinatura.
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }
    }
}
