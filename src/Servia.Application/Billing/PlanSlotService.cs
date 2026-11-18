using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Application.Billing;

/// <summary>
/// Executa transições de elegibilidade e valida o aumento efetivo de ocupação
/// na mesma transação. A operação deve ler e persistir usando o contexto
/// compartilhado pelos repositórios desta unidade de trabalho.
/// </summary>
public sealed class PlanSlotService(
    ISubscriptionRepository subscriptions,
    IPlanRepository plans,
    IProfessionalCountReader professionalCounts)
{
    public Task<T> ExecuteWithSlotCheckAsync<T>(
        Guid tenantId,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return subscriptions.ExecuteSerializableAsync(async token =>
        {
            var before = await professionalCounts.CountEligibleAsync(tenantId, token);
            var result = await operation(token);
            var after = await professionalCounts.CountEligibleAsync(tenantId, token);

            // Operações idempotentes ou sem aumento continuam permitidas,
            // inclusive quando o plano está cheio.
            if (after > before)
            {
                var subscription = await subscriptions.FindCurrentAsync(tenantId, token);
                if (subscription is not null)
                {
                    var plan = await plans.FindByIdAsync(subscription.PlanId, token);
                    if (plan is not null && !plan.AllowsProfessionals(after))
                        throw new DomainRuleException(
                            $"O plano {plan.Name} comporta até {plan.IncludedProfessionals} profissionais. "
                            + "Faça upgrade do plano para habilitar mais profissionais.");
                }
            }
            return result;
        }, cancellationToken);
    }
}
