using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Servia.Domain.Common;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;
using Servia.Infrastructure.Repositories;
using Servia.Infrastructure.Tenancy;

namespace Servia.Tests.Persistence;

public sealed class SubscriptionCheckoutPersistenceTests : IAsyncLifetime
{
    private static readonly DateOnly Today = new(2026, 11, 18);
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly PostgresTestDatabase _database = new();
    private readonly Plan _trialPlan = Plan.Create("E3-TRIAL", "Trial", Money.From(99m), 1, SiteCustomizationLevel.Basic, 10);
    private readonly Plan _paidPlan = Plan.Create("E3-PAID", "Pago", Money.From(199m), 3, SiteCustomizationLevel.Full, 50);

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        await using var db = Open(_tenant);
        db.Plans.AddRange(_trialPlan, _paidPlan);
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => _database.DisposeAsync();

    private AppDbContext Open(Guid tenant)
    {
        var context = new TenantContext();
        context.SetTenant(tenant);
        return _database.CreateContext(context);
    }

    [Fact]
    public async Task Tentativas_Concorrentes_Gravam_Uma_Unica_Chave_E_Impedem_Segunda_Tentativa_Aberta()
    {
        var key = Guid.NewGuid();
        var bothRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readers = 0;

        async Task<Guid?> TryStartAsync()
        {
            await using var db = Open(_tenant);
            var repository = new SubscriptionRepository(db);
            try
            {
                return await repository.ExecuteSerializableAsync(async token =>
                {
                    Assert.Null(await repository.FindCheckoutAsync(_tenant, key, token));
                    if (Interlocked.Increment(ref readers) == 2)
                    {
                        bothRead.SetResult();
                    }
                    await bothRead.Task.WaitAsync(TimeSpan.FromSeconds(15), token);
                    var checkout = Subscription.StartCheckout(_tenant, _paidPlan, key, Today);
                    await repository.AddAsync(checkout, token);
                    await repository.SaveChangesAsync(token);
                    return checkout.Id;
                });
            }
            catch (ConcurrencyConflictException)
            {
                return null;
            }
        }

        var persistedId = Assert.Single((await Task.WhenAll(TryStartAsync(), TryStartAsync())).OfType<Guid>());
        await using var verification = Open(_tenant);
        var reader = new SubscriptionRepository(verification);
        Assert.Equal(persistedId, (await reader.FindCheckoutAsync(_tenant, key))!.Id);
        Assert.Null(await reader.FindCurrentAsync(_tenant));
        verification.Subscriptions.Add(Subscription.StartCheckout(_tenant, _paidPlan, Guid.NewGuid(), Today));
        var rejected = await Assert.ThrowsAsync<DbUpdateException>(() => verification.SaveChangesAsync());
        Assert.Equal("UX_Subscriptions_PendingTenant", Assert.IsType<PostgresException>(rejected.InnerException).ConstraintName);
    }

    [Fact]
    public async Task Ativacao_Enquanto_Trial_Vigente_Reverte_E_Plano_Pendente_Nao_Concede_Beneficios()
    {
        await using var db = Open(_tenant);
        var repository = new SubscriptionRepository(db);
        var trial = TestSubscriptionFactory.AuthorizedTrial(_tenant, _trialPlan.Id, Today.AddDays(-14));
        var checkout = Subscription.StartCheckout(_tenant, _paidPlan, Guid.NewGuid(), Today.AddDays(-14));
        checkout.UseAuthorizedTrial("trial-plan", _trialPlan.Id);
        checkout.MarkCheckoutDispatched(Subscription.AtUtc(Today.AddDays(-14)));
        checkout.BindCheckout("external");
        checkout.ConfirmAuthorization("external");
        checkout.BeginAuthorizedTrial("external", Subscription.AtUtc(Today.AddDays(-14)), Subscription.AtUtc(Today));
        checkout.ExpireTrial(Today);
        db.Subscriptions.AddRange(trial, checkout);
        await db.SaveChangesAsync();
        Assert.Equal(trial.Id, (await repository.FindCurrentAsync(_tenant))!.Id);
        Assert.Equal(trial.Id, (await new AiUsageRepository(db, repository).FindCurrentSubscriptionAsync(_tenant))!.Id);

        var change = PlanChange.Record(_tenant, checkout.Id, null, _paidPlan.Id, PlanChangeReason.Initial,
            Guid.NewGuid(), DateTimeOffset.UtcNow);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => repository.ExecuteSerializableAsync(async token =>
        {
            await repository.AddChangeAsync(change, token);
            await repository.SaveChangesAsync(token);
            checkout.ConfirmFirstPayment("external", "approved", _paidPlan, 0, Today, Today.AddMonths(1));
            await repository.SaveChangesAsync(token);
            return true;
        }));

        Assert.Empty(await repository.ListChangesAsync(_tenant));
        var unchanged = (await repository.FindCheckoutAsync(_tenant, checkout.CheckoutKey!.Value))!;
        Assert.Equal(SubscriptionStatus.AuthorizedAwaitingFirstPayment, unchanged.Status);
        Assert.Null(unchanged.FirstPaymentId);

    }

    [Fact]
    public async Task Historico_De_Falha_Reserva_Id_Externo_E_Isola_Chave_Por_Tenant()
    {
        var key = Guid.NewGuid();
        await using var db = Open(_tenant);
        var failed = Subscription.StartCheckout(_tenant, _paidPlan, key, Today);
        failed.BindCheckout("external");
        failed.FailCheckout(Today);
        db.Subscriptions.Add(failed);
        await db.SaveChangesAsync();

        var otherTenant = Guid.NewGuid();
        await using var other = Open(otherTenant);
        var repository = new SubscriptionRepository(other);
        Assert.Null(await repository.FindCheckoutAsync(_tenant, key));
        Assert.Null(await repository.FindCheckoutAsync(otherTenant, key));
        var second = Subscription.StartCheckout(otherTenant, _paidPlan, key, Today);
        second.BindCheckout("external");
        other.Subscriptions.Add(second);
        var rejected = await Assert.ThrowsAsync<DbUpdateException>(() => other.SaveChangesAsync());
        Assert.Equal("IX_Subscriptions_GatewaySubscriptionId",
            Assert.IsType<PostgresException>(rejected.InnerException).ConstraintName);
        Assert.Null(await new SubscriptionRepository(db).FindCurrentAsync(_tenant));
        var retry = Subscription.StartCheckout(_tenant, _paidPlan, Guid.NewGuid(), Today);
        db.Subscriptions.Add(retry);
        await db.SaveChangesAsync();
        Assert.Equal(2, await db.Subscriptions.CountAsync());
        retry.FailCheckout(Today);
        await db.SaveChangesAsync();
        db.Subscriptions.Add(Subscription.StartCheckout(_tenant, _paidPlan, key, Today));
        var duplicateKey = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal("IX_Subscriptions_TenantId_CheckoutKey",
            Assert.IsType<PostgresException>(duplicateKey.InnerException).ConstraintName);
    }

    [Fact]
    public async Task Confirmacao_Desatualizada_Nao_Sobrescreve_Falha_Persistida()
    {
        var key = Guid.NewGuid();
        await using var db = Open(_tenant);
        var checkout = Subscription.StartCheckout(_tenant, _paidPlan, key, Today);
        checkout.BindCheckout("external");
        db.Subscriptions.Add(checkout);
        await db.SaveChangesAsync();

        await using var stale = Open(_tenant);
        var staleRepository = new SubscriptionRepository(stale);
        var outdated = (await staleRepository.FindCheckoutAsync(_tenant, key))!;
        checkout.FailCheckout(Today);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => staleRepository.ExecuteSerializableAsync(async token =>
        {
            outdated.ConfirmAuthorization("external");
            await staleRepository.SaveChangesAsync(token);
            return true;
        }));

        var persisted = (await staleRepository.FindCheckoutAsync(_tenant, key))!;
        Assert.Equal(SubscriptionStatus.Cancelled, persisted.Status);
        Assert.Equal(Today, persisted.CheckoutFailedOn);
    }

    [Fact]
    public async Task Migration_Incremental_Preserva_Trial_E_Historico_Legados()
    {
        await using var db = Open(_tenant);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261116052646_AiTenantIdentifierAndOperationCost");
        var id = Guid.NewGuid();
        var historyId = Guid.NewGuid();
        var changedAt = DateTimeOffset.UtcNow;
        var changedBy = Guid.NewGuid();
        var end = Today.AddDays(14);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Subscriptions" ("Id", "TenantId", "PlanId", "Status", "CurrentPeriodStart", "CurrentPeriodEnd", "TrialEndsAt")
            VALUES ({id}, {_tenant}, {_trialPlan.Id}, 'Trialing', {Today}, {end}, {end})
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "PlanChanges" ("Id", "TenantId", "SubscriptionId", "ToPlanId", "Reason", "ChangedBy", "ChangedAt")
            VALUES ({historyId}, {_tenant}, {id}, {_trialPlan.Id}, 'Initial', {changedBy}, {changedAt})
            """);
        await migrator.MigrateAsync();
        var repository = new SubscriptionRepository(db);
        var trial = (await repository.FindCurrentAsync(_tenant))!;
        Assert.Equal(id, trial.Id);
        Assert.Equal(end, trial.TrialEndsAt);
        Assert.Equal(SubscriptionStatus.Trialing, trial.Status);
        Assert.Null(trial.ContractPriceAmount);
        var change = Assert.Single(await repository.ListChangesAsync(_tenant));
        Assert.Equal(historyId, change.Id);
        Assert.Equal(PlanChangeStatus.Effective, change.Status);

        db.Subscriptions.Add(TestSubscriptionFactory.AuthorizedTrial(_tenant, _paidPlan.Id, Today));
        var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal("UX_Subscriptions_EffectiveTenant",
            Assert.IsType<PostgresException>(duplicate.InnerException).ConstraintName);
    }
}
