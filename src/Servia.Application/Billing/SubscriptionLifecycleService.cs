using Servia.Domain.Billing;
using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Application.Billing;

public sealed class SubscriptionLifecycleService(
    ISubscriptionRepository repository,
    IPlanRepository plans,
    IProfessionalCountReader professionals,
    IEnumerable<IRecurringCheckoutGateway> gateways,
    TimeProvider clock)
{
    private readonly IRecurringCheckoutGateway? _gateway = gateways.SingleOrDefault();

    public async Task<LifecycleResult> CancelAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var snapshot = await CurrentAsync(tenantId, cancellationToken);
        if (snapshot.Status == SubscriptionStatus.Cancelled || snapshot.CancellationEffectiveOn.HasValue)
        {
            return new(snapshot, snapshot.Status == SubscriptionStatus.Cancelled ? "Cancelled" : "CancellationScheduled");
        }
        if (snapshot.GatewaySubscriptionId is null)
        {
            throw new DomainRuleException("O cancelamento exige confirmação da recorrência.");
        }
        var stored = await repository.ExecuteSerializableAsync(async token =>
        {
            var current = await CurrentAsync(tenantId, token, readOnly: false);
            EnsureSame(snapshot, current);
            if (current.Status != SubscriptionStatus.Cancelled && !current.CancellationRequestedOn.HasValue)
            {
                current.RequestCancellation(Today(clock.GetUtcNow()));
                await repository.SaveChangesAsync(token);
            }
            return current;
        }, cancellationToken);
        if (stored.Status == SubscriptionStatus.Cancelled) return new(stored, "Cancelled");
        if (_gateway is not { } gateway || stored.CheckoutKey is null)
        {
            return new(stored, "CancellationPending");
        }
        var remote = await ReadAuthorizationAsync(stored, gateway, cancellationToken);
        if (remote is null) return new(stored, "CancellationPending");
        if (Cancelled(remote)) return await ConfirmRequestedCancellationAsync(stored, remote, cancellationToken);
        if (remote.Status is not ("authorized" or "paused")) return new(stored, "CancellationPending");
        var dispatch = await repository.ExecuteSerializableAsync(async token =>
        {
            var current = await CurrentAsync(tenantId, token, readOnly: false);
            EnsureSame(snapshot, current);
            if (current.Status == SubscriptionStatus.Cancelled || current.CancellationDispatchedAtUtc.HasValue
                || current.CancellationEffectiveOn.HasValue)
            {
                return false;
            }
            current.MarkCancellationDispatched(clock.GetUtcNow());
            await repository.SaveChangesAsync(token);
            return true;
        }, cancellationToken);

        if (dispatch)
        {
            try
            {
                await gateway.RequestCancellationAsync(stored.GatewaySubscriptionId!, cancellationToken);
            }
            catch (RecurringCheckoutGatewayException)
            {
                return new(stored, "CancellationPending");
            }
            remote = await ReadAuthorizationAsync(stored, gateway, cancellationToken);
        }
        return remote is not null && Cancelled(remote)
            ? await ConfirmRequestedCancellationAsync(stored, remote, cancellationToken)
            : new(stored, "CancellationPending");
    }

    private async Task<LifecycleResult> ConfirmRequestedCancellationAsync(Subscription snapshot,
        RecurringAuthorization observed, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        if (snapshot.FirstPaymentId is null && snapshot.TrialDeadline <= now)
        {
            return new(snapshot, "CancellationPending");
        }
        if (snapshot.FirstPaymentId is not null && snapshot.ChargeDeadline <= now)
        {
            try
            {
                return await VerifyAsync(snapshot.TenantId, cancellationToken, snapshot.Id, observedAuthorization: observed);
            }
            catch (RecurringCheckoutGatewayException)
            {
                return new(snapshot, "CancellationPending");
            }
        }
        return await ConfirmCancellationAsync(snapshot, cancellationToken);
    }

    public Task<PlanChange> RequestChangeAsync(Guid tenantId, Guid userId, string targetCode,
        CancellationToken cancellationToken) =>
        repository.ExecuteSerializableAsync(async token =>
        {
            var current = await CurrentAsync(tenantId, token, readOnly: false);
            var target = await plans.FindByCodeAsync(targetCode, token) ?? throw NotFoundException.For("Plano", targetCode);
            var pending = await PendingChangeAsync(current, token);
            if (pending is not null)
            {
                return pending.ToPlanId == target.Id ? pending
                    : throw new ConcurrencyConflictException("Já existe uma alteração de plano pendente.");
            }
            var now = clock.GetUtcNow();
            if (current.GatewaySubscriptionId is null || current.FirstPaymentId is null
                || current.ChargeDeadline <= now || current.CancellationRequestedOn.HasValue)
            {
                throw new DomainRuleException("A assinatura não permite solicitar alteração neste momento.");
            }
            var from = await plans.FindByIdAsync(current.PlanId, token) ?? throw NotFoundException.For("Plano", current.PlanId);
            var count = await professionals.CountEligibleAsync(tenantId, token);
            var change = PlanChange.Request(current, from, target, count, userId, now);
            await repository.AddChangeAsync(change, token);
            await repository.SaveChangesAsync(token);
            return change;
        }, cancellationToken);

    public async Task<LifecycleResult> VerifyAsync(Guid tenantId, CancellationToken cancellationToken,
        Guid? expectedSubscriptionId = null, DateTimeOffset? expectedDue = null,
        RecurringAuthorization? observedAuthorization = null)
    {
        var snapshot = await CurrentAsync(tenantId, cancellationToken);
        if (expectedSubscriptionId.HasValue && snapshot.Id != expectedSubscriptionId)
        {
            return new(snapshot, "Superseded");
        }
        if (snapshot.Status == SubscriptionStatus.Cancelled)
        {
            return new(snapshot, "Cancelled");
        }
        if (_gateway is not { } gateway || snapshot.GatewaySubscriptionId is null || snapshot.CheckoutKey is null)
        {
            return new(snapshot, "RequiresReconciliation");
        }
        var backgroundVerification = observedAuthorization is not null;
        if (snapshot.CancellationRequestedOn.HasValue && observedAuthorization is null)
        {
            observedAuthorization = await ReadAuthorizationAsync(snapshot, gateway, cancellationToken);
        }
        var cancellationObserved = false;
        if (observedAuthorization is not null)
        {
            if (!Matches(snapshot, observedAuthorization))
            {
                return new(snapshot, "RequiresReconciliation");
            }
            cancellationObserved = Cancelled(observedAuthorization);
            if (cancellationObserved && (snapshot.FirstPaymentId is null || snapshot.CancellationEffectiveOn.HasValue
                || snapshot.ChargeDeadline is not { } charge || charge > clock.GetUtcNow()))
            {
                return await ConfirmCancellationAsync(snapshot, cancellationToken, observedExternally: true);
            }
        }
        if (snapshot.CancellationRequestedOn.HasValue && !cancellationObserved)
        {
            return new(snapshot, "CancellationPending");
        }
        if (snapshot.Status is not (SubscriptionStatus.Active or SubscriptionStatus.PastDue)
            || snapshot.ChargeDeadline is not { } due)
        {
            return new(snapshot, "AwaitingFirstPayment");
        }
        if (!cancellationObserved && expectedDue.HasValue && expectedDue != due)
        {
            return new(snapshot, expectedDue < due ? "PreviousCycle" : "AwaitingCycle");
        }
        if (clock.GetUtcNow() < due)
        {
            return new(snapshot, expectedDue.HasValue ? "AwaitingCycle" : "Current");
        }
        if (snapshot.ContractPriceAmount is not { } price || snapshot.ContractCurrency is not { } currency
            || snapshot.BillingIntervalMonths is not { } months)
        {
            return new(snapshot, "RequiresReconciliation");
        }

        var authorization = observedAuthorization ?? await ReadAuthorizationAsync(snapshot, gateway, cancellationToken);
        var amount = Money.From(price, currency);
        var pending = await PendingChangeAsync(snapshot, cancellationToken);
        PlanChange? downgrade = null;
        if (!cancellationObserved && pending is { Reason: PlanChangeReason.Downgrade, RequestedPrice: { } targetPrice, RequestedCurrency: { } targetCurrency }
            && pending.EffectiveAtUtc == due && authorization?.Amount == targetPrice && authorization.Currency == targetCurrency)
        {
            downgrade = pending;
            amount = Money.From(targetPrice, targetCurrency);
        }
        if (!cancellationObserved && (authorization is not { Status: "authorized" } || authorization.Amount != amount.Amount
            || authorization.Currency != amount.Currency || authorization.Frequency != months
            || authorization.FrequencyType != "months"))
        {
            return new(snapshot, "RequiresReconciliation");
        }

        RecurringCycleEvidence evidence;
        try
        {
            evidence = await gateway.FindCyclePaymentAsync(snapshot.GatewaySubscriptionId, due, amount, cancellationToken);
        }
        catch (RecurringCheckoutGatewayException) when (!backgroundVerification)
        {
            return new(snapshot, cancellationObserved ? "CancellationPending" : "RequiresReconciliation");
        }
        if (cancellationObserved && evidence.Payment is null && !evidence.NoInvoice && !evidence.Rejected)
        {
            return new(snapshot, "CancellationPending");
        }
        if (cancellationObserved && evidence.NoInvoice)
        {
            return await ConfirmCancellationAsync(snapshot, cancellationToken, observedExternally: true, verifiedDue: due);
        }

        return await repository.ExecuteSerializableAsync(async token =>
        {
            var current = await CurrentAsync(tenantId, token, readOnly: false);
            EnsureSame(snapshot, current);
            if (current.ChargeDeadline != due)
            {
                return new LifecycleResult(current, "Current");
            }
            if ((!cancellationObserved && current.CancellationRequestedOn.HasValue)
                || current.Status == SubscriptionStatus.Cancelled || current.CancellationEffectiveOn.HasValue)
            {
                return new LifecycleResult(current, "RequiresReconciliation");
            }
            var now = clock.GetUtcNow();
            var end = due.ToUniversalTime().AddMonths(months);
            var payment = evidence.Payment;
            Plan? target = null;
            PlanChange? confirmedChange = null;
            if (payment is not null)
            {
                if (payment.DueAt != due || payment.ApprovedAt < due || payment.ApprovedAt > now
                    || await repository.IsRecurringPaymentUsedAsync(payment.Id, token))
                {
                    return new LifecycleResult(current, "RequiresReconciliation");
                }
                if (downgrade is not null)
                {
                    confirmedChange = await PendingChangeAsync(current, token);
                    if (confirmedChange?.Id != downgrade.Id || confirmedChange.FromPlanId != current.PlanId)
                    {
                        return new LifecycleResult(current, "RequiresReconciliation");
                    }
                    target = await plans.FindByIdAsync(confirmedChange.ToPlanId, token);
                    if (target is not { IsActive: true }
                        || !target.AllowsProfessionals(await professionals.CountEligibleAsync(tenantId, token)))
                    {
                        return new LifecycleResult(current, "CapacityExceeded");
                    }
                }
            }
            var invoice = await repository.FindRecurringInvoiceAsync(tenantId, current.Id, due, token);
            if (invoice is null)
            {
                invoice = PlatformInvoice.CreateRecurring(tenantId, current.Id, due, end, amount);
                await repository.AddRecurringInvoiceAsync(invoice, token);
            }
            else if (invoice.Total != amount)
            {
                return new LifecycleResult(current, "RequiresReconciliation");
            }

            if (payment is null)
            {
                current.MarkPastDue(due);
                if (evidence.Rejected) invoice.RecordRecurringFailure(due);
            }
            else
            {
                invoice.MarkPaid(payment.Id, payment.ApprovedAt.ToUniversalTime());
                current.ConfirmRenewal(snapshot.GatewaySubscriptionId, payment.Id, due, end);
                if (target is not null)
                {
                    current.ApplyConfirmedPlan(target, await professionals.CountEligibleAsync(tenantId, token), amount);
                    confirmedChange!.ConfirmScheduledDowngrade(due, now);
                }
            }
            if (cancellationObserved)
            {
                current.RequestCancellation(Today(now));
                if (payment is null || end > now)
                {
                    ConfirmCancellation(current, now);
                }
            }
            await repository.SaveChangesAsync(token);
            if (cancellationObserved && current.CancellationEffectiveOn.HasValue)
            {
                return new LifecycleResult(current, current.Status == SubscriptionStatus.Cancelled
                    ? "Cancelled" : "CancellationScheduled");
            }
            return new LifecycleResult(current,
                payment is not null ? "Renewed" : evidence.Rejected ? "PaymentRejected" : "PaymentPending");
        }, cancellationToken);
    }

    private async Task<LifecycleResult> ConfirmCancellationAsync(Subscription snapshot, CancellationToken cancellationToken,
        bool observedExternally = false, DateTimeOffset? verifiedDue = null) =>
        await repository.ExecuteSerializableAsync(async token =>
        {
            var current = await CurrentAsync(snapshot.TenantId, token, readOnly: false);
            EnsureSame(snapshot, current);
            if (verifiedDue.HasValue && current.ChargeDeadline != verifiedDue)
            {
                return new LifecycleResult(current, "Current");
            }
            if (current.Status != SubscriptionStatus.Cancelled && current.CancellationEffectiveOn is null)
            {
                if (observedExternally && current.CancellationRequestedOn is null)
                {
                    current.RequestCancellation(Today(clock.GetUtcNow()));
                }
                ConfirmCancellation(current, clock.GetUtcNow());
            }
            if (current.Status != SubscriptionStatus.Cancelled && current.CancellationDeadline <= clock.GetUtcNow())
            {
                current.EndCancellation(clock.GetUtcNow());
            }
            if (current.CancellationEffectiveOn.HasValue)
            {
                await repository.SaveChangesAsync(token);
            }
            return new LifecycleResult(current, current.Status == SubscriptionStatus.Cancelled ? "Cancelled" : "CancellationScheduled");
        }, cancellationToken);

    private static void ConfirmCancellation(Subscription current, DateTimeOffset now)
    {
        current.ConfirmCancellation(current.GatewaySubscriptionId!, now);
        if (current.Status != SubscriptionStatus.Cancelled && current.CancellationDeadline <= now)
        {
            current.EndCancellation(now);
        }
    }

    private async Task<Subscription> CurrentAsync(Guid tenantId, CancellationToken cancellationToken, bool readOnly = true) =>
        await repository.FindCurrentAsync(tenantId, cancellationToken, readOnly)
            ?? throw NotFoundException.For("Assinatura", tenantId);

    private async Task<PlanChange?> PendingChangeAsync(Subscription current, CancellationToken cancellationToken) =>
        (await repository.ListChangesAsync(current.TenantId, cancellationToken))
            .FirstOrDefault(change => change.SubscriptionId == current.Id && change.Status == PlanChangeStatus.Requested);

    private static async Task<RecurringAuthorization?> ReadAuthorizationAsync(Subscription subscription,
        IRecurringCheckoutGateway gateway, CancellationToken cancellationToken)
    {
        try
        {
            var remote = await gateway.FindAsync(subscription.GatewaySubscriptionId!, cancellationToken);
            return Matches(subscription, remote) ? remote : null;
        }
        catch (RecurringCheckoutGatewayException)
        {
            return null;
        }
    }

    private static bool Matches(Subscription subscription, RecurringAuthorization remote) =>
        remote.Id == subscription.GatewaySubscriptionId && remote.ExternalReference == subscription.Id.ToString("N");

    private static bool Cancelled(RecurringAuthorization authorization) => authorization.Status is "cancelled" or "canceled";
    private static DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(now.UtcDateTime);

    private static void EnsureSame(Subscription expected, Subscription current)
    {
        if (expected.Id != current.Id || expected.GatewaySubscriptionId != current.GatewaySubscriptionId)
        {
            throw new ConcurrencyConflictException("A assinatura vigente foi alterada.");
        }
    }
}

public sealed record LifecycleResult(Subscription Subscription, string State);
