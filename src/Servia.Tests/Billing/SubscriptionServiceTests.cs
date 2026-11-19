using Servia.Application.Billing;
using Servia.Domain.Common;
using Servia.Domain.Tenancy;
using Servia.Tests.Common;


namespace Servia.Tests.Billing;

/// <summary>
/// F16 — billing com escrita.
///
/// Contratar, trocar e cancelar plano, com a regra de faixa de
/// profissionais. Testes de domínio e de caso de uso, sem banco.
/// </summary>
public class SubscriptionServiceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeSubscriptionRepository _subscriptions = new();
    private readonly FakePlanRepository _plans = new();
    private readonly FakeProfessionalCountReader _counts = new();

    private SubscriptionService NewService() =>
        new(_subscriptions, _plans, _counts, new FixedTimeProvider(Now));

    private void SeedPlans()
    {
        _plans.Add(Plan.Create("ESSENCIAL", "Essencial", Money.From(99m), 3,
            SiteCustomizationLevel.Basic, 100));
        _plans.Add(Plan.Create("PRO", "Pro", Money.From(199m), 7,
            SiteCustomizationLevel.Intermediate, 500));
        _plans.Add(Plan.Create("BUSINESS", "Business", Money.From(499m), 0,
            SiteCustomizationLevel.Full, 5000));
    }

    private async Task<Subscription> SeedTrialAsync(string planCode)
    {
        var plan = (await _plans.FindByCodeAsync(planCode))!;
        var trial = Subscription.StartTrial(TenantId, plan.Id, DateOnly.FromDateTime(Now.UtcDateTime));
        await _subscriptions.AddAsync(trial);
        return trial;
    }

    [Fact]
    public async Task Contratacao_E_Troca_Locais_Nao_Gravam_Assinatura_Ou_Historico()
    {
        SeedPlans();
        var service = NewService();
        var subscribe = await service.SubscribeAsync(TenantId, "PRO", UserId);
        Assert.False(subscribe.Success);
        Assert.Contains("confirmação financeira", subscribe.FailureReason);
        Assert.Null(await _subscriptions.FindCurrentAsync(TenantId));

        var trial = await SeedTrialAsync("ESSENCIAL");
        var change = await service.ChangePlanAsync(TenantId, "PRO", UserId);
        Assert.False(change.Success);
        Assert.Contains("confirmação financeira", change.FailureReason);
        Assert.Equal((await _plans.FindByCodeAsync("ESSENCIAL"))!.Id, trial.PlanId);
        Assert.Equal(SubscriptionStatus.Trialing, trial.Status);
        Assert.Empty(await _subscriptions.ListChangesAsync(TenantId));
    }
    [Fact]
    public async Task Sugestao_De_Upgrade_Indica_O_Plano_Necessario()
    {
        SeedPlans();
        var service = NewService();
        await SeedTrialAsync("ESSENCIAL");

        // 10 profissionais: Essencial (3) nao cabe, Pro (7) nao cabe,
        // Business (ilimitado) cabe.
        _counts.Set(TenantId, 10);

        var suggestion = await service.SuggestUpgradeAsync(TenantId);

        Assert.NotNull(suggestion);
        Assert.Equal("ESSENCIAL", suggestion!.CurrentPlan.Code);
        Assert.Equal("BUSINESS", suggestion.RequiredPlan.Code);
        Assert.Equal(10, suggestion.ProfessionalCount);
    }

    [Fact]
    public async Task Sem_Excedente_Nao_Ha_Sugestao()
    {
        SeedPlans();
        var service = NewService();
        await SeedTrialAsync("PRO");

        _counts.Set(TenantId, 5);

        Assert.Null(await service.SuggestUpgradeAsync(TenantId));
    }

    // ---------- F16.3: cancelar ----------

    [Fact]
    public async Task Cancelar_Assinatura()
    {
        SeedPlans();
        var service = NewService();
        await SeedTrialAsync("ESSENCIAL");

        var result = await service.CancelAsync(TenantId);

        Assert.True(result.Success);
        Assert.Equal(SubscriptionStatus.Cancelled, result.Subscription!.Status);
        Assert.Equal(DateOnly.FromDateTime(Now.UtcDateTime), result.Subscription.CancellationEffectiveOn);
    }

    [Fact]
    public async Task Cancelamento_Pago_Local_Nao_Altera_Periodo_Ou_Estado()
    {
        SeedPlans();
        var plan = (await _plans.FindByCodeAsync("ESSENCIAL"))!;
        var today = DateOnly.FromDateTime(Now.UtcDateTime);
        var paid = Subscription.StartCheckout(TenantId, plan, Guid.NewGuid(), today);
        paid.BindCheckout("subscription");
        paid.ConfirmAuthorization("subscription");
        paid.ConfirmFirstPayment("subscription", "paid", plan, 0, today, today.AddMonths(1));
        await _subscriptions.AddAsync(paid);

        var result = await NewService().CancelAsync(TenantId);

        Assert.False(result.Success);
        Assert.Contains("confirmação financeira", result.FailureReason);
        Assert.Equal(SubscriptionStatus.Active, paid.Status);
        Assert.Equal(today.AddMonths(1), paid.CurrentPeriodEnd);
        Assert.Null(paid.CancellationRequestedOn);
    }

    [Fact]
    public async Task Cancelar_Sem_Assinatura_Falha()
    {
        SeedPlans();

        var result = await NewService().CancelAsync(TenantId);

        Assert.False(result.Success);
    }

}

/// <summary>Repositório de assinaturas em memória.</summary>
internal sealed class FakeSubscriptionRepository : ISubscriptionRepository
{
    private readonly List<Subscription> _subscriptions = [];
    private readonly List<PlanChange> _changes = [];

    public Task<Subscription?> FindCurrentAsync(
        Guid tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_subscriptions
            .Where(subscription => subscription.TenantId == tenantId)
            .OrderByDescending(subscription => subscription.CurrentPeriodStart)
            .FirstOrDefault());

    public Task<Subscription?> FindCheckoutAsync(Guid tenantId, Guid checkoutKey,
        CancellationToken cancellationToken = default, bool readOnly = false) =>
        Task.FromResult(_subscriptions.SingleOrDefault(item => item.TenantId == tenantId && item.CheckoutKey == checkoutKey));

    public Task<bool> HasPendingCheckoutAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_subscriptions.Any(item => item.TenantId == tenantId
            && item.Status is SubscriptionStatus.PendingCheckout or SubscriptionStatus.PendingConfirmation
                or SubscriptionStatus.AuthorizedAwaitingFirstPayment));

    public Task AddAsync(Subscription subscription, CancellationToken cancellationToken = default)
    {
        _subscriptions.Add(subscription);
        return Task.CompletedTask;
    }

    public Task AddChangeAsync(PlanChange change, CancellationToken cancellationToken = default)
    {
        _changes.Add(change);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PlanChange>> ListChangesAsync(
        Guid tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PlanChange>>(
            _changes.Where(change => change.TenantId == tenantId).ToList());

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <summary>
    /// Em memória não há transação real: a operação roda diretamente.
    /// A atomicidade é provada nos testes de integração com PostgreSQL.
    /// </summary>
    public Task<T> ExecuteSerializableAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default) =>
        operation(cancellationToken);
}

/// <summary>Repositório de planos em memória.</summary>
internal sealed class FakePlanRepository : IPlanRepository
{
    private readonly List<Plan> _plans = [];

    public void Add(Plan plan) => _plans.Add(plan);

    public Task<Plan?> FindByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        Task.FromResult(_plans.FirstOrDefault(
            plan => string.Equals(plan.Code, code, StringComparison.OrdinalIgnoreCase)));

    public Task<Plan?> FindByIdAsync(Guid planId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_plans.FirstOrDefault(plan => plan.Id == planId));

    public Task<Plan?> FindSmallestForAsync(
        int professionalCount, CancellationToken cancellationToken = default) =>
        Task.FromResult(_plans
            .Where(plan => plan.IsActive)
            .OrderBy(plan => plan.IncludedProfessionals == 0
                ? int.MaxValue
                : plan.IncludedProfessionals)
            .FirstOrDefault(plan => plan.AllowsProfessionals(professionalCount)));
}

/// <summary>Leitor de contagem de profissionais em memória.</summary>
internal sealed class FakeProfessionalCountReader : IProfessionalCountReader
{
    private readonly Dictionary<Guid, int> _counts = [];

    public void Set(Guid tenantId, int count) => _counts[tenantId] = count;

    public Task<int> CountEligibleAsync(
        Guid tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_counts.GetValueOrDefault(tenantId));
}
