using Microsoft.EntityFrameworkCore;
using Servia.Domain.Common;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;
using Servia.Infrastructure.Tenancy;

namespace Servia.Tests.Persistence;

/// <summary>
/// Etapa 2 da auditoria — isolamento multi-tenant de <see cref="Subscription"/>.
///
/// Antes da correção, <c>Subscription</c> não implementava
/// <see cref="ITenantScoped"/> e o filtro global não se aplicava: um
/// estabelecimento podia ler a assinatura de outro.
///
/// Estes testes provam o isolamento em leitura, criação e alteração,
/// contra PostgreSQL 17.
/// </summary>
public class SubscriptionIsolationTests : IAsyncLifetime
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 11, 11);

    private readonly PostgresTestDatabase _database = new();

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();

        // O seed verifica o banco antes de inserir: e idempotente por si.
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

    /// <summary>Cria uma assinatura para o tenant informado.</summary>
    private async Task<Subscription> CreateSubscriptionAsync(Guid tenantId)
    {
        await using var context = _database.CreateContext(ContextFor(tenantId));

        var plan = await context.Plans.FirstAsync(plan => plan.Code == "ESSENCIAL");
        var subscription = Subscription.StartTrial(tenantId, plan.Id, Today);

        context.Subscriptions.Add(subscription);
        await context.SaveChangesAsync();

        return subscription;
    }

    // ---------- Leitura ----------

    [Fact]
    public async Task Cada_Tenant_Ve_Apenas_A_Propria_Assinatura()
    {
        var mine = await CreateSubscriptionAsync(TenantA);
        await CreateSubscriptionAsync(TenantB);

        await using var context = _database.CreateContext(ContextFor(TenantA));

        var visible = await context.Subscriptions.ToListAsync();

        Assert.Single(visible);
        Assert.Equal(mine.Id, visible[0].Id);
        Assert.Equal(TenantA, visible[0].TenantId);
    }

    [Fact]
    public async Task Consulta_Por_Id_De_Outro_Tenant_Nao_Retorna_Nada()
    {
        await CreateSubscriptionAsync(TenantA);
        var other = await CreateSubscriptionAsync(TenantB);

        await using var context = _database.CreateContext(ContextFor(TenantA));

        // O filtro global impede enxergar a assinatura do outro tenant,
        // mesmo conhecendo o identificador.
        var found = await context.Subscriptions
            .FirstOrDefaultAsync(subscription => subscription.Id == other.Id);

        Assert.Null(found);
    }

    [Fact]
    public async Task Sem_Tenant_Resolvido_Nenhuma_Assinatura_E_Visivel()
    {
        await CreateSubscriptionAsync(TenantA);

        await using var context = _database.CreateContext(new TenantContext());

        var visible = await context.Subscriptions.ToListAsync();

        Assert.Empty(visible);
    }

    // ---------- Alteração ----------

    [Fact]
    public async Task Alteracao_Em_Assinatura_De_Outro_Tenant_Nao_E_Encontrada()
    {
        await CreateSubscriptionAsync(TenantA);
        var other = await CreateSubscriptionAsync(TenantB);

        await using var context = _database.CreateContext(ContextFor(TenantA));

        var found = await context.Subscriptions
            .FirstOrDefaultAsync(subscription => subscription.Id == other.Id);

        Assert.Null(found);
    }

    [Fact]
    public async Task Cancelamento_De_Outro_Tenant_Nao_Afeta_O_Registro()
    {
        await CreateSubscriptionAsync(TenantA);
        var other = await CreateSubscriptionAsync(TenantB);

        // Tenant A tenta cancelar a assinatura de B pelo identificador.
        await using (var context = _database.CreateContext(ContextFor(TenantA)))
        {
            var found = await context.Subscriptions
                .FirstOrDefaultAsync(subscription => subscription.Id == other.Id);

            // Nao encontra: o filtro global bloqueia antes de qualquer alteracao.
            Assert.Null(found);
        }

        // A assinatura de B permanece intacta.
        await using (var context = _database.CreateContext(ContextFor(TenantB)))
        {
            var stillThere = await context.Subscriptions
                .FirstOrDefaultAsync(subscription => subscription.Id == other.Id);

            Assert.NotNull(stillThere);
            Assert.Equal(SubscriptionStatus.Trialing, stillThere!.Status);
        }
    }

    // ---------- Criação ----------

    [Fact]
    public async Task Assinatura_Criada_Pertence_Ao_Tenant_Do_Contexto()
    {
        var created = await CreateSubscriptionAsync(TenantA);

        Assert.Equal(TenantA, created.TenantId);

        await using var context = _database.CreateContext(ContextFor(TenantB));

        var visibleToB = await context.Subscriptions
            .FirstOrDefaultAsync(subscription => subscription.Id == created.Id);

        Assert.Null(visibleToB);
    }

    [Fact]
    public async Task Tenant_Com_Assinatura_Nao_Ve_A_Do_Outro()
    {
        var a = await CreateSubscriptionAsync(TenantA);
        var b = await CreateSubscriptionAsync(TenantB);

        await using var contextA = _database.CreateContext(ContextFor(TenantA));
        await using var contextB = _database.CreateContext(ContextFor(TenantB));

        var visibleA = await contextA.Subscriptions.SingleAsync();
        var visibleB = await contextB.Subscriptions.SingleAsync();

        Assert.Equal(a.Id, visibleA.Id);
        Assert.Equal(b.Id, visibleB.Id);
        Assert.NotEqual(visibleA.Id, visibleB.Id);
    }
}
