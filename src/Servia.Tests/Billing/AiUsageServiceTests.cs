using Servia.Application.Billing;
using Servia.Domain.Billing;
using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Tests.Billing;

/// <summary>
/// Política provisória de consumo de IA.
///
/// 1 crédito = 10.000 tokens (entrada + saída). O consumo é acumulado em
/// tokens com precisão; ao atingir a franquia, bloqueia; sem cobrança de
/// excedente; renovação mensal zera o consumo.
/// </summary>
public class AiUsageServiceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 11, 13);

    private readonly FakeAiUsageRepository _repository = new();
    private readonly FakePlanRepository _plans = new();

    private AiUsageService NewService() =>
        new(_repository, _plans, TimeProvider.System);

    private void SeedSubscriptionWithPlan(int includedAiUnits)
    {
        var plan = Plan.Create(
            "PRO", "Pro", Money.From(199m), 7, SiteCustomizationLevel.Intermediate, includedAiUnits);

        _plans.Add(plan);
        _repository.SetSubscription(
            Subscription.StartTrial(TenantId, plan.Id, Today));
    }

    private Task<UsageQuota?> QuotaAsync() =>
        _repository.FindAsync(TenantId, QuotaResource.AiUnits);

    // ---------- Conversão e criação ----------

    [Fact]
    public async Task Cria_Cota_A_Partir_Do_Plano_Em_Creditos()
    {
        SeedSubscriptionWithPlan(includedAiUnits: 500);

        await NewService().EnsureAvailableAsync(TenantId);

        var quota = await QuotaAsync();

        Assert.NotNull(quota);
        Assert.Equal(500, quota!.Included);
        Assert.Equal(5_000_000, quota.AiTokenLimit);
    }

    [Fact]
    public void Um_Credito_Equivale_A_Dez_Mil_Tokens()
    {
        Assert.Equal(10_000, UsageQuota.AiTokensPerCredit);
    }

    [Fact]
    public async Task Converte_Tokens_Em_Creditos()
    {
        SeedSubscriptionWithPlan(includedAiUnits: 100);

        var service = NewService();

        // 25.000 tokens = 2,5 créditos → 2 créditos inteiros.
        await service.RecordConsumptionAsync(TenantId, 25_000);

        var quota = await QuotaAsync();

        Assert.Equal(25_000, quota!.ConsumedTokens);
        Assert.Equal(2, quota.Consumed);
    }

    // ---------- Acumulação precisa ----------

    [Fact]
    public async Task Acumula_Sem_Arredondar_Cada_Operacao()
    {
        SeedSubscriptionWithPlan(includedAiUnits: 100);

        var service = NewService();

        // 6.000 + 6.000 = 12.000 tokens = 1,2 créditos.
        // Se arredondasse por operação, cada 6.000 viraria 0 crédito.
        await service.RecordConsumptionAsync(TenantId, 6_000);
        await service.RecordConsumptionAsync(TenantId, 6_000);

        var quota = await QuotaAsync();

        Assert.Equal(12_000, quota!.ConsumedTokens);
        Assert.Equal(1, quota.Consumed);
    }

    [Fact]
    public async Task Acumula_Muitas_Operacoes_Pequenas()
    {
        SeedSubscriptionWithPlan(includedAiUnits: 100);

        var service = NewService();

        // 10 operações de 1.000 tokens = 10.000 = 1 crédito.
        for (var i = 0; i < 10; i++)
        {
            await service.RecordConsumptionAsync(TenantId, 1_000);
        }

        var quota = await QuotaAsync();

        Assert.Equal(10_000, quota!.ConsumedTokens);
        Assert.Equal(1, quota.Consumed);
    }

    // ---------- Bloqueio ----------

    [Fact]
    public async Task Bloqueia_Quando_Franquia_Esgotada()
    {
        SeedSubscriptionWithPlan(includedAiUnits: 1);

        var service = NewService();
        await service.RecordConsumptionAsync(TenantId, 10_000);

        await Assert.ThrowsAsync<DomainRuleException>(
            () => service.EnsureAvailableAsync(TenantId));
    }

    [Fact]
    public async Task Nao_Bloqueia_Enquanto_Houver_Saldo()
    {
        SeedSubscriptionWithPlan(includedAiUnits: 1);

        var service = NewService();
        await service.RecordConsumptionAsync(TenantId, 9_999);

        // Ainda há 1 token de saldo: não bloqueia.
        await service.EnsureAvailableAsync(TenantId);
    }

    // ---------- Sem cobrança de excedente ----------

    [Fact]
    public async Task Nao_Cobra_Excedente_Ao_Ultrapassar()
    {
        SeedSubscriptionWithPlan(includedAiUnits: 1);

        var service = NewService();

        // Consome mais que a franquia: o total é limitado, sem cobrança.
        await service.RecordConsumptionAsync(TenantId, 25_000);

        var quota = await QuotaAsync();

        Assert.Equal(10_000, quota!.ConsumedTokens);
        Assert.Equal(1, quota.Consumed);
        Assert.False(BillingRules.ShouldBillConsumption(quota));
    }

    // ---------- Renovação mensal ----------

    [Fact]
    public async Task Renovacao_Mensal_Zera_O_Consumo()
    {
        SeedSubscriptionWithPlan(includedAiUnits: 1);

        var service = NewService();
        await service.RecordConsumptionAsync(TenantId, 10_000);

        var quota = await QuotaAsync();
        Assert.False(quota!.HasAiCapacity);

        // Novo período: consumo e tokens zerados, franquia renovada.
        quota.Reset(Today.AddMonths(1), Today.AddMonths(2), included: 1);

        Assert.Equal(0, quota.Consumed);
        Assert.Equal(0, quota.ConsumedTokens);
        Assert.True(quota.HasAiCapacity);
    }

    // ---------- Sem assinatura ----------

    [Fact]
    public async Task Sem_Assinatura_Nao_Bloqueia()
    {
        await NewService().EnsureAvailableAsync(TenantId);
    }
}

/// <summary>Repositório de cotas em memória.</summary>
internal sealed class FakeAiUsageRepository : IAiUsageRepository
{
    private readonly List<UsageQuota> _quotas = [];
    private Subscription? _subscription;

    public void SetSubscription(Subscription subscription) => _subscription = subscription;

    public Task<UsageQuota?> FindAsync(
        Guid tenantId, QuotaResource resource, CancellationToken cancellationToken = default) =>
        Task.FromResult(_quotas.FirstOrDefault(
            quota => quota.TenantId == tenantId && quota.Resource == resource));

    public Task<Subscription?> FindCurrentSubscriptionAsync(
        Guid tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_subscription);

    public Task AddAsync(UsageQuota quota, CancellationToken cancellationToken = default)
    {
        _quotas.Add(quota);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
