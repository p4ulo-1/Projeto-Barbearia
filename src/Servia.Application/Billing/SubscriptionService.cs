using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Application.Billing;

/// <summary>
/// Casos de uso de assinatura (F16).
///
/// Protege as operações públicas de assinatura e consulta o plano efetivo.
///
/// <b>Regra de faixa (F16.4):</b> o plano define quantos profissionais o
/// tenant pode ter. Ao ultrapassar o limite, o tenant precisa subir de
/// categoria — não há cobrança por excedente.
/// </summary>
public sealed class SubscriptionService(
    ISubscriptionRepository subscriptions,
    IPlanRepository plans,
    IProfessionalCountReader professionalCounts)
{
    private const string FinancialConfirmationRequired =
        "Operação temporariamente indisponível até que a confirmação financeira esteja habilitada.";

    public Task<SubscriptionResult> SubscribeAsync(
        Guid tenantId, string planCode, Guid requestedBy, CancellationToken cancellationToken = default) =>
        Task.FromResult(SubscriptionResult.Invalid(FinancialConfirmationRequired));

    public Task<SubscriptionResult> ChangePlanAsync(
        Guid tenantId, string planCode, Guid requestedBy, CancellationToken cancellationToken = default) =>
        Task.FromResult(SubscriptionResult.Invalid(FinancialConfirmationRequired));

    public Task<Subscription?> FindCurrentAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        subscriptions.FindCurrentAsync(tenantId, cancellationToken);

    /// <summary>
    /// Verifica se a equipe atual excede a faixa do plano e, se sim,
    /// indica o menor plano que a comporta (F16.4).
    /// </summary>
    public async Task<PlanUpgradeSuggestion?> SuggestUpgradeAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await subscriptions.FindCurrentAsync(tenantId, cancellationToken);

        if (subscription is null)
        {
            return null;
        }

        var currentPlan = await plans.FindByIdAsync(subscription.EffectivePlanId, cancellationToken);

        if (currentPlan is null)
        {
            return null;
        }

        var professionalCount = await professionalCounts.CountEligibleAsync(
            tenantId, cancellationToken);

        if (currentPlan.AllowsProfessionals(professionalCount))
        {
            return null;
        }

        var required = await plans.FindSmallestForAsync(professionalCount, cancellationToken);

        return required is null
            ? null
            : new PlanUpgradeSuggestion(currentPlan, required, professionalCount);
    }
}

/// <summary>Resultado de uma operação de assinatura.</summary>
public sealed record SubscriptionResult(
    bool Success,
    Subscription? Subscription,
    Plan? Plan,
    PlanChange? Change,
    string? FailureReason)
{
    public static SubscriptionResult Succeeded(
        Subscription subscription, Plan? plan, PlanChange? change) =>
        new(true, subscription, plan, change, null);

    public static SubscriptionResult Invalid(string reason) =>
        new(false, null, null, null, reason);
}

/// <summary>Sugestão de upgrade por excedente de profissionais.</summary>
public sealed record PlanUpgradeSuggestion(
    Plan CurrentPlan,
    Plan RequiredPlan,
    int ProfessionalCount);
