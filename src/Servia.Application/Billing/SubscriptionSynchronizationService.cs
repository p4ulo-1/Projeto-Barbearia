using Servia.Domain.Billing;
using Servia.Domain.Tenancy;

namespace Servia.Application.Billing;

public sealed class SubscriptionSynchronizationService(SubscriptionCheckoutService checkout,
    SubscriptionLifecycleService lifecycle, SubscriptionPlanChangeService planChanges, TimeProvider clock)
{
    public async Task<LifecycleResult> ApplyAsync(Subscription subscription, RecurringAuthorization authorization,
        CancellationToken cancellationToken, DateTimeOffset? expectedDue = null, bool authorizationOnly = false)
    {
        if (subscription.CheckoutKey is not { } key || authorization.Id is null
            || authorization.ExternalReference != subscription.Id.ToString("N")
            || authorization.Status is not ("authorized" or "pending" or "paused" or "cancelled" or "canceled"))
        {
            return new(subscription, "RequiresReconciliation");
        }
        if (subscription.Status is SubscriptionStatus.Cancelled or SubscriptionStatus.Expired)
        {
            return new(subscription, "Superseded");
        }
        if (subscription.GatewaySubscriptionId is null
            && !await checkout.BindRecoveredAsync(subscription.TenantId, key, authorization, cancellationToken))
        {
            return new(subscription, "RequiresReconciliation");
        }
        if (subscription.GatewaySubscriptionId is { } stored && stored != authorization.Id)
        {
            return new(subscription, "RequiresReconciliation");
        }

        var cancelled = authorization.Status is "cancelled" or "canceled";
        if (!cancelled)
        {
            var pending = await planChanges.SynchronizeAsync(subscription.TenantId, subscription.Id,
                cancellationToken, propagateGatewayError: true);
            if (pending && subscription.ChargeDeadline > clock.GetUtcNow() && !authorizationOnly)
                return new(subscription, "PlanChangePending");
        }
        if (subscription.FirstPaymentId is not null || (subscription.IsOnTrial && subscription.TrialDeadline > clock.GetUtcNow()
            && (cancelled || subscription.CancellationRequestedOn.HasValue)))
        {
            return authorizationOnly && !cancelled && subscription.CancellationRequestedOn is null
                ? new(subscription, "Current")
                : await lifecycle.VerifyAsync(subscription.TenantId, cancellationToken, subscription.Id,
                    expectedDue, authorization);
        }
        if (!cancelled && expectedDue > clock.GetUtcNow()) return new(subscription, "AwaitingCycle");
        if (!cancelled && expectedDue.HasValue && subscription.TrialEndsAtUtc is { } trialEnd && trialEnd != expectedDue)
        {
            return new(subscription, expectedDue < trialEnd ? "PreviousCycle" : "AwaitingCycle");
        }
        var verified = await checkout.VerifyAsync(subscription.TenantId, null, key, cancellationToken, authorization);
        return new(verified.Attempt, verified.State);
    }
}
