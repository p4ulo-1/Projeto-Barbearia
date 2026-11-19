using Servia.Domain.Ai;
using Servia.Domain.Billing;
using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Application.Billing;

/// <summary>
/// Consumo de créditos de IA do período (gestão de IA, §5).
///
/// <b>Ordem de consumo (§5.4):</b> a franquia mensal do plano é consumida
/// antes dos créditos adicionais. Os créditos adicionais vivem em lotes
/// independentes (<see cref="AiCreditLot"/>), cada um com sua validade; o
/// lote que expira antes é consumido primeiro e créditos expirados são
/// ignorados.
///
/// <b>Bloqueio:</b> ao esgotar franquia e lotes válidos, novas operações são
/// bloqueadas. Não há cobrança ilimitada por excedente.
///
/// <b>Auditoria (§5.3):</b> cada operação gera um <see cref="AiOperationLog"/>
/// com tenant, tipo, provedor, tokens e resultado — sem conteúdo de prompt
/// ou resposta.
///
/// A cota é criada sob demanda a partir do plano vigente, no período da
/// assinatura. O consumo real vem do provedor (tokens de entrada e saída).
/// </summary>
public sealed class AiUsageService(
    IAiUsageRepository repository,
    IPlanRepository plans,
    IAiPricing pricing,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Garante que há saldo de IA para a operação. Lança
    /// <see cref="DomainRuleException"/> quando franquia e créditos
    /// adicionais válidos estão esgotados.
    /// </summary>
    public async Task EnsureAvailableAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var quota = await ResolveQuotaAsync(tenantId, cancellationToken);

        if (quota is null)
        {
            // Sem assinatura/plano: não há franquia a aplicar.
            return;
        }

        if (quota.HasAiCapacity)
        {
            return;
        }

        // Franquia esgotada: só há saldo se existir lote válido.
        var lots = await repository.FindConsumableLotsAsync(
            tenantId, timeProvider.GetUtcNow(), cancellationToken);

        if (lots.Count == 0)
        {
            throw new DomainRuleException(
                "A franquia mensal de IA foi atingida e não há créditos adicionais válidos. "
                + "Novas operações ficam bloqueadas até o próximo período ou até a compra de créditos.");
        }
    }

    /// <summary>
    /// Registra o consumo real da operação, em <b>tokens</b> (entrada + saída,
    /// como o provedor informou). O total é acumulado com precisão e
    /// convertido para créditos (10.000 tokens por crédito) sem arredondar
    /// cada operação.
    ///
    /// A franquia é consumida primeiro; o que ultrapassar é debitado dos
    /// lotes adicionais válidos, do que expira antes para o que expira depois.
    /// </summary>
    public async Task RecordConsumptionAsync(
        Guid tenantId,
        long tokens,
        CancellationToken cancellationToken = default)
    {
        if (tokens <= 0)
        {
            return;
        }

        var quota = await ResolveQuotaAsync(tenantId, cancellationToken);

        if (quota is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();

        // Saldo dos lotes válidos: é o que permite consumir além da franquia.
        var lots = await repository.FindConsumableLotsAsync(tenantId, now, cancellationToken);
        var extraAvailable = lots.Sum(lot => lot.Remaining);

        // Créditos consumidos além da franquia antes desta operação.
        var beyondBefore = quota.ConsumedBeyondIncluded;

        quota.AccumulateAiTokens(tokens, extraAvailable);

        // Créditos que esta operação consumiu além da franquia.
        var beyondAfter = quota.ConsumedBeyondIncluded;
        var extraConsumed = beyondAfter - beyondBefore;

        if (extraConsumed > 0)
        {
            ConsumeLots(lots, extraConsumed, now);
        }

        await repository.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Registra a operação de IA para auditoria e reconciliação (§5.4).
    ///
    /// Calcula o custo conservador a partir dos tokens reais, com os preços de
    /// pico configurados, e registra a referência da tabela de preços usada.
    /// Não persiste conteúdo de prompt nem de resposta: apenas metadados de
    /// consumo e resultado.
    /// </summary>
    public async Task RecordOperationAsync(
        Guid tenantId,
        AiOperationType operationType,
        string providerName,
        string keyLabel,
        long inputTokens,
        long outputTokens,
        bool success,
        string? failureReason = null,
        CancellationToken cancellationToken = default)
    {
        // Custo conservador: entrada como cache miss, precos de pico (§2.3).
        var cost = pricing.CalculateCost(inputTokens, outputTokens);

        var log = AiOperationLog.Record(
            tenantId,
            operationType,
            providerName,
            pricing.Model,
            keyLabel,
            (int)Math.Min(int.MaxValue, inputTokens),
            (int)Math.Min(int.MaxValue, outputTokens),
            cost,
            pricing.PriceTableVersion,
            success,
            timeProvider.GetUtcNow(),
            failureReason);

        await repository.AddOperationLogAsync(log, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Debita os créditos adicionais dos lotes válidos, do que expira antes
    /// para o que expira depois (§5.4). Lotes expirados não são consumidos.
    /// </summary>
    private static void ConsumeLots(
        IReadOnlyList<AiCreditLot> lots,
        int credits,
        DateTimeOffset now)
    {
        var remaining = credits;

        foreach (var lot in lots)
        {
            if (remaining <= 0)
            {
                break;
            }

            remaining -= lot.Consume(remaining, now);
        }
    }

    /// <summary>
    /// Cota de IA do período atual, criada a partir do plano quando ainda
    /// não existe. Retorna nulo quando o tenant não tem assinatura/plano.
    /// </summary>
    private async Task<UsageQuota?> ResolveQuotaAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var existing = await repository.FindAsync(
            tenantId, QuotaResource.AiUnits, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var subscription = await repository.FindCurrentSubscriptionAsync(
            tenantId, cancellationToken);

        if (subscription is null)
        {
            return null;
        }

        var plan = await plans.FindByIdAsync(subscription.EffectivePlanId, cancellationToken);

        if (plan is null)
        {
            return null;
        }

        var quota = UsageQuota.Create(
            tenantId,
            QuotaResource.AiUnits,
            subscription.CurrentPeriodStart,
            subscription.CurrentPeriodEnd,
            plan.IncludedAiUnits);

        await repository.AddAsync(quota, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return quota;
    }
}
