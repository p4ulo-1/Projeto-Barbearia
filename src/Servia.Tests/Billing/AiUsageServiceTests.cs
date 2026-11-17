using Servia.Application.Billing;
using Servia.Domain.Ai;
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
    private readonly FakeAiPricing _pricing = new();

    private AiUsageService NewService() =>
        new(_repository, _plans, _pricing, TimeProvider.System);

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

    // ---------- Lotes de créditos adicionais (§5.4, §7.1) ----------

    private AiCreditLot SeedLot(int quantity, int daysToExpire)
    {
        var now = DateTimeOffset.UtcNow;

        var lot = AiCreditLot.Create(
            TenantId,
            quantity,
            Money.From(quantity * 0.03m),
            discountPercent: 0m,
            acquiredAt: now,
            expiresAt: now.AddDays(daysToExpire));

        _repository.AddLotAsync(lot).GetAwaiter().GetResult();

        return lot;
    }

    [Fact]
    public async Task Franquia_Eh_Consumida_Antes_Dos_Lotes()
    {
        SeedSubscriptionWithPlan(includedAiUnits: 1);
        var lot = SeedLot(quantity: 5, daysToExpire: 30);

        // 10.000 tokens = 1 crédito: consome exatamente a franquia.
        await NewService().RecordConsumptionAsync(TenantId, 10_000);

        Assert.Equal(0, lot.Consumed);
    }

    [Fact]
    public async Task Excedente_Debita_O_Lote()
    {
        SeedSubscriptionWithPlan(includedAiUnits: 1);
        var lot = SeedLot(quantity: 5, daysToExpire: 30);

        // 30.000 tokens = 3 créditos: 1 da franquia, 2 do lote.
        await NewService().RecordConsumptionAsync(TenantId, 30_000);

        Assert.Equal(2, lot.Consumed);
        Assert.Equal(3, lot.Remaining);
    }

    [Fact]
    public async Task Lote_Que_Expira_Antes_Eh_Consumido_Primeiro()
    {
        SeedSubscriptionWithPlan(includedAiUnits: 1);

        var later = SeedLot(quantity: 5, daysToExpire: 90);
        var sooner = SeedLot(quantity: 5, daysToExpire: 10);

        // 30.000 tokens = 3 créditos: 1 da franquia, 2 do lote que expira antes.
        await NewService().RecordConsumptionAsync(TenantId, 30_000);

        Assert.Equal(2, sooner.Consumed);
        Assert.Equal(0, later.Consumed);
    }

    [Fact]
    public async Task Lote_Expirado_Nao_Eh_Consumido()
    {
        SeedSubscriptionWithPlan(includedAiUnits: 1);

        var expired = AiCreditLot.Create(
            TenantId,
            5,
            Money.From(0.15m),
            0m,
            DateTimeOffset.UtcNow.AddDays(-60),
            DateTimeOffset.UtcNow.AddDays(-1));

        await _repository.AddLotAsync(expired);

        // Franquia esgotada e o único lote está expirado: bloqueia.
        await NewService().RecordConsumptionAsync(TenantId, 10_000);

        await Assert.ThrowsAsync<DomainRuleException>(
            () => NewService().EnsureAvailableAsync(TenantId));

        Assert.Equal(0, expired.Consumed);
    }

    [Fact]
    public async Task Lote_Valido_Libera_Operacao_Apos_Franquia()
    {
        SeedSubscriptionWithPlan(includedAiUnits: 1);
        SeedLot(quantity: 5, daysToExpire: 30);

        await NewService().RecordConsumptionAsync(TenantId, 10_000);

        // Franquia esgotada, mas há lote válido: não bloqueia.
        await NewService().EnsureAvailableAsync(TenantId);
    }

    // ---------- Registro de operação (§5.3) ----------

    [Fact]
    public async Task Registra_Operacao_Com_Tokens_E_Resultado()
    {
        SeedSubscriptionWithPlan(includedAiUnits: 100);

        await NewService().RecordOperationAsync(
            TenantId,
            AiOperationType.SiteGeneration,
            "deepseek",
            "test-key",
            inputTokens: 1_200,
            outputTokens: 800,
            success: true);

        var log = Assert.Single(_repository.Logs);

        Assert.Equal(TenantId, log.TenantId);
        Assert.Equal(AiOperationType.SiteGeneration, log.OperationType);
        Assert.Equal("deepseek", log.ProviderName);
        Assert.Equal(2_000, log.UnitsConsumed);
        Assert.True(log.Success);
    }

    [Fact]
    public async Task Registra_Falha_Com_Motivo()
    {
        SeedSubscriptionWithPlan(includedAiUnits: 100);

        await NewService().RecordOperationAsync(
            TenantId,
            AiOperationType.SiteRefinement,
            "deepseek",
            "test-key",
            inputTokens: 0,
            outputTokens: 0,
            success: false,
            failureReason: "O provedor não retornou conteúdo.");

        var log = Assert.Single(_repository.Logs);

        Assert.False(log.Success);
        Assert.Equal("O provedor não retornou conteúdo.", log.FailureReason);
    }
}

/// <summary>Repositório de cotas em memória.</summary>
internal sealed class FakeAiUsageRepository : IAiUsageRepository
{
    private readonly List<UsageQuota> _quotas = [];
    private readonly List<AiCreditLot> _lots = [];
    private readonly List<AiOperationLog> _logs = [];
    private Subscription? _subscription;

    public IReadOnlyList<AiCreditLot> Lots => _lots;

    public IReadOnlyList<AiOperationLog> Logs => _logs;

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

    public Task<IReadOnlyList<AiCreditLot>> FindConsumableLotsAsync(
        Guid tenantId, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AiCreditLot>>(
            _lots
                .Where(lot => lot.TenantId == tenantId && lot.IsConsumable(now))
                .OrderBy(lot => lot.ExpiresAt)
                .ToList());

    public Task AddLotAsync(AiCreditLot lot, CancellationToken cancellationToken = default)
    {
        _lots.Add(lot);
        return Task.CompletedTask;
    }

    public Task AddOperationLogAsync(
        AiOperationLog log, CancellationToken cancellationToken = default)
    {
        _logs.Add(log);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

/// <summary>
/// Precos de pico para teste (gestao de IA, §2.1): entrada US$ 0,30 e saida
/// US$ 1,20 por milhao de tokens.
/// </summary>
internal sealed class FakeAiPricing : IAiPricing
{
    public string Model => "deepseek-flash";

    public decimal InputPricePerMillionTokens => 0.30m;

    public decimal OutputPricePerMillionTokens => 1.20m;

    public string PriceTableVersion => "test-peak";

    public int MaxOutputTokens => 4_000;

    public decimal CalculateCost(long inputTokens, long outputTokens)
    {
        const decimal million = 1_000_000m;

        return (inputTokens / million * InputPricePerMillionTokens)
               + (outputTokens / million * OutputPricePerMillionTokens);
    }

    public decimal EstimateMaxCost(int estimatedInputTokens) =>
        CalculateCost(Math.Max(0, estimatedInputTokens), MaxOutputTokens);
}
