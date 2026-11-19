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
    IProfessionalCountReader professionalCounts,
    TimeProvider timeProvider)
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

    /// <summary>Encerra o trial local. O cancelamento pago depende de confirmação externa.</summary>
    public Task<SubscriptionResult> CancelAsync(
        Guid tenantId, CancellationToken cancellationToken = default) =>
        subscriptions.ExecuteSerializableAsync(token => CancelTrialAsync(tenantId, token), cancellationToken);

    private async Task<SubscriptionResult> CancelTrialAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var subscription = await subscriptions.FindCurrentAsync(tenantId, cancellationToken);

        if (subscription is null)
        {
            return SubscriptionResult.Invalid("O estabelecimento não possui assinatura.");
        }

        if (!subscription.IsOnTrial && subscription.TrialEndsAt is null)
        {
            return SubscriptionResult.Invalid(FinancialConfirmationRequired);
        }

        try
        {
            var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
            subscription.CancelTrial(today);
        }
        catch (DomainRuleException exception)
        {
            return SubscriptionResult.Invalid(exception.Message);
        }

        await subscriptions.SaveChangesAsync(cancellationToken);

        var plan = await plans.FindByIdAsync(subscription.EffectivePlanId, cancellationToken);

        return SubscriptionResult.Succeeded(subscription, plan, null);
    }

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
