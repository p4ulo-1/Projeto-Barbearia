using Servia.Domain.Billing;
using Servia.Domain.Tenancy;

namespace Servia.Application.Billing;

public enum BillingAccess
{
    Operational,
    NoNewBookings,
    OwnerReadOnly
}

public sealed class SubscriptionAccessService(ISubscriptionRepository subscriptions, TimeProvider clock)
{
    public async Task<BillingAccess> GetAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var current = await subscriptions.FindCurrentAsync(tenantId, cancellationToken, readOnly: true);
        var now = clock.GetUtcNow();
        // Cadastros novos sempre possuem um registro explícito aguardando autorização.
        if (current is null)
        {
            return BillingAccess.Operational;
        }
        if (current.IsOnTrial)
        {
            return !current.CancellationRequestedOn.HasValue && current.TrialDeadline > now
                ? BillingAccess.Operational : BillingAccess.OwnerReadOnly;
        }
        if (current.Status is not (SubscriptionStatus.Active or SubscriptionStatus.PastDue)
            || current.CancellationDeadline <= now)
        {
            return BillingAccess.OwnerReadOnly;
        }
        var failedAt = current.PastDueSince ?? current.ChargeDeadline;
        if (failedAt is not { } failure || now < failure)
        {
            return current.IsActive ? BillingAccess.Operational : BillingAccess.OwnerReadOnly;
        }
        return DelinquencyPolicy.CanWrite(DelinquencyPolicy.Evaluate(failure, now))
            ? BillingAccess.NoNewBookings : BillingAccess.OwnerReadOnly;
    }
}

public sealed class BillingAccessDeniedException()
    : Exception("A situação da assinatura impede esta operação.");
