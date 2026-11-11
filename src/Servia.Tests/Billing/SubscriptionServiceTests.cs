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

    // ---------- F16.1: contratar ----------

    [Fact]
    public async Task Contratar_Plano_Cria_Assinatura_Em_Trial()
    {
        SeedPlans();

        var result = await NewService().SubscribeAsync(TenantId, "ESSENCIAL", UserId);

        Assert.True(result.Success);
        Assert.Equal(SubscriptionStatus.Trialing, result.Subscription!.Status);
        Assert.Equal("ESSENCIAL", result.Plan!.Code);
    }

    [Fact]
    public async Task Contratar_Registra_Mudanca_Inicial()
    {
        SeedPlans();

        var result = await NewService().SubscribeAsync(TenantId, "ESSENCIAL", UserId);

        Assert.NotNull(result.Change);
        Assert.Equal(PlanChangeReason.Initial, result.Change!.Reason);
        Assert.Null(result.Change.FromPlanId);
    }

    [Fact]
    public async Task Contratar_Plano_Inexistente_Falha()
    {
        SeedPlans();

        var result = await NewService().SubscribeAsync(TenantId, "INEXISTENTE", UserId);

        Assert.False(result.Success);
        Assert.Contains("não encontrado", result.FailureReason);
    }

    [Fact]
    public async Task Contratar_Duas_Vezes_Falha()
    {
        SeedPlans();
        var service = NewService();

        await service.SubscribeAsync(TenantId, "ESSENCIAL", UserId);
        var second = await service.SubscribeAsync(TenantId, "PRO", UserId);

        Assert.False(second.Success);
        Assert.Contains("já possui assinatura", second.FailureReason);
    }

    // ---------- F16.2: trocar ----------

    [Fact]
    public async Task Trocar_Para_Plano_Maior_Eh_Upgrade()
    {
        SeedPlans();
        var service = NewService();
        await service.SubscribeAsync(TenantId, "ESSENCIAL", UserId);

        var result = await service.ChangePlanAsync(TenantId, "PRO", UserId);

        Assert.True(result.Success);
        Assert.Equal(PlanChangeReason.Upgrade, result.Change!.Reason);
        Assert.Equal("PRO", result.Plan!.Code);
    }

    [Fact]
    public async Task Trocar_Para_Plano_Menor_Eh_Downgrade()
    {
        SeedPlans();
        var service = NewService();
        await service.SubscribeAsync(TenantId, "PRO", UserId);

        var result = await service.ChangePlanAsync(TenantId, "ESSENCIAL", UserId);

        Assert.True(result.Success);
        Assert.Equal(PlanChangeReason.Downgrade, result.Change!.Reason);
    }

    [Fact]
    public async Task Trocar_Para_O_Mesmo_Plano_Falha()
    {
        SeedPlans();
        var service = NewService();
        await service.SubscribeAsync(TenantId, "ESSENCIAL", UserId);

        var result = await service.ChangePlanAsync(TenantId, "ESSENCIAL", UserId);

        Assert.False(result.Success);
        Assert.Contains("é o atual", result.FailureReason);
    }

    [Fact]
    public async Task Trocar_Sem_Assinatura_Falha()
    {
        SeedPlans();

        var result = await NewService().ChangePlanAsync(TenantId, "PRO", UserId);

        Assert.False(result.Success);
        Assert.Contains("não possui assinatura", result.FailureReason);
    }

    // ---------- F16.4: regra de faixa ----------

    [Fact]
    public async Task Downgrade_Com_Equipe_Acima_Da_Faixa_Falha()
    {
        SeedPlans();
        var service = NewService();
        await service.SubscribeAsync(TenantId, "PRO", UserId);

        // 5 profissionais: cabe no Pro (7), nao cabe no Essencial (3).
        _counts.Set(TenantId, 5);

        var result = await service.ChangePlanAsync(TenantId, "ESSENCIAL", UserId);

        Assert.False(result.Success);
        Assert.Contains("comporta até 3", result.FailureReason);
    }

    [Fact]
    public async Task Downgrade_Com_Equipe_Dentro_Da_Faixa_Eh_Aceito()
    {
        SeedPlans();
        var service = NewService();
        await service.SubscribeAsync(TenantId, "PRO", UserId);

        _counts.Set(TenantId, 3);

        var result = await service.ChangePlanAsync(TenantId, "ESSENCIAL", UserId);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task Plano_Ilimitado_Comporta_Qualquer_Equipe()
    {
        SeedPlans();
        var service = NewService();
        await service.SubscribeAsync(TenantId, "ESSENCIAL", UserId);

        _counts.Set(TenantId, 200);

        var result = await service.ChangePlanAsync(TenantId, "BUSINESS", UserId);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task Sugestao_De_Upgrade_Indica_O_Plano_Necessario()
    {
        SeedPlans();
        var service = NewService();
        await service.SubscribeAsync(TenantId, "ESSENCIAL", UserId);

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
        await service.SubscribeAsync(TenantId, "PRO", UserId);

        _counts.Set(TenantId, 5);

        Assert.Null(await service.SuggestUpgradeAsync(TenantId));
    }

    // ---------- F16.3: cancelar ----------

    [Fact]
    public async Task Cancelar_Assinatura()
    {
        SeedPlans();
        var service = NewService();
        await service.SubscribeAsync(TenantId, "ESSENCIAL", UserId);

        var result = await service.CancelAsync(TenantId);

        Assert.True(result.Success);
        Assert.Equal(SubscriptionStatus.Cancelled, result.Subscription!.Status);
    }

    [Fact]
    public async Task Cancelar_Sem_Assinatura_Falha()
    {
        SeedPlans();

        var result = await NewService().CancelAsync(TenantId);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Assinatura_Cancelada_Nao_Troca_De_Plano()
    {
        SeedPlans();
        var service = NewService();
        await service.SubscribeAsync(TenantId, "ESSENCIAL", UserId);
        await service.CancelAsync(TenantId);

        var result = await service.ChangePlanAsync(TenantId, "PRO", UserId);

        Assert.False(result.Success);
        Assert.Contains("cancelada", result.FailureReason);
    }

    // ---------- F16.5: historico ----------

    [Fact]
    public async Task Historico_Registra_Todas_As_Mudancas()
    {
        SeedPlans();
        var service = NewService();

        await service.SubscribeAsync(TenantId, "ESSENCIAL", UserId);
        await service.ChangePlanAsync(TenantId, "PRO", UserId);
        await service.ChangePlanAsync(TenantId, "ESSENCIAL", UserId);

        var history = await _subscriptions.ListChangesAsync(TenantId);

        Assert.Equal(3, history.Count);
        Assert.Contains(history, change => change.Reason == PlanChangeReason.Initial);
        Assert.Contains(history, change => change.Reason == PlanChangeReason.Upgrade);
        Assert.Contains(history, change => change.Reason == PlanChangeReason.Downgrade);
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

    public Task<int> CountActiveAsync(
        Guid tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_counts.GetValueOrDefault(tenantId));
}
