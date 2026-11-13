using Servia.Domain.Ai;
using Servia.Domain.Billing;
using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Application.Billing;

/// <summary>
/// Consumo de créditos de IA do período (política provisória).
///
/// <b>Regra provisória:</b> a franquia mensal do plano é tratada como
/// créditos. Ao esgotar, novas operações são <b>bloqueadas</b>; não há
/// cobrança automática de excedente. A arquitetura já suporta créditos
/// adicionais (<see cref="UsageQuota.AddExtra"/>), mas a contratação não
/// está ativa.
///
/// A cota é criada sob demanda a partir do plano vigente, no período da
/// assinatura. O consumo real vem do provedor (total de tokens).
/// </summary>
public sealed class AiUsageService(
    IAiUsageRepository repository,
    IPlanRepository plans,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Garante que há saldo de IA para a operação. Lança
    /// <see cref="DomainRuleException"/> quando a franquia está esgotada.
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

        if (!quota.HasAiCapacity)
        {
            throw new DomainRuleException(
                "A franquia mensal de IA foi atingida. "
                + "Novas operações ficam bloqueadas até o próximo período.");
        }
    }

    /// <summary>
    /// Registra o consumo real da operação, em <b>tokens</b> (entrada + saída,
    /// <c>total_tokens</c> do provedor). O total é acumulado com precisão e
    /// convertido para créditos (10.000 tokens por crédito) sem arredondar
    /// cada operação. Nunca ultrapassa o limite: o excedente não é cobrado.
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

        quota.AccumulateAiTokens(tokens);

        await repository.SaveChangesAsync(cancellationToken);
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

        var plan = await plans.FindByIdAsync(subscription.PlanId, cancellationToken);

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
