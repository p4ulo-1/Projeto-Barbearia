using Servia.Domain.Billing;
using Servia.Domain.Common;
using Servia.Domain.Identity;
using Servia.Domain.Tenancy;

namespace Servia.Application.Billing;

public sealed class SubscriptionCheckoutService(
    ISubscriptionRepository subscriptions,
    IPlanRepository plans,
    IProfessionalCountReader professionals,
    IUserRepository users,
    IEnumerable<IRecurringCheckoutGateway> gateways,
    TimeProvider clock)
{
    private readonly IRecurringCheckoutGateway? _gateway = gateways.SingleOrDefault();

    public bool IsConfigured => _gateway is not null;
    public bool CanStartCheckout => _gateway is { CanStartCheckout: true };

    public async Task<CheckoutResult> StartAsync(Guid tenantId, Guid userId, Guid key, string planCode,
        CancellationToken cancellationToken, string? cardToken = null)
    {
        if (tenantId == Guid.Empty || key == Guid.Empty)
        {
            throw new DomainRuleException("Estabelecimento e chave da contratação são obrigatórios.");
        }

        var gateway = RequireGateway();
        if (!gateway.CanStartCheckout)
        {
            throw new InvalidOperationException("A contratação recorrente está indisponível.");
        }
        var user = await users.FindByIdAsync(tenantId, userId, cancellationToken);
        if (user is not { IsActive: true })
        {
            throw NotFoundException.For("Usuário", userId);
        }

        var plan = await plans.FindByCodeAsync(planCode, cancellationToken)
            ?? throw NotFoundException.For("Plano", planCode);
        var saved = await subscriptions.FindCheckoutAsync(tenantId, key, cancellationToken, readOnly: true);
        if (saved is not null && (saved.CheckoutDispatchedAt.HasValue || saved.GatewaySubscriptionId is not null
            || saved.Status != SubscriptionStatus.PendingCheckout))
        {
            CheckReplayPlan(saved, plan);
            return await DescribeAsync(saved, cancellationToken);
        }
        if (string.IsNullOrWhiteSpace(cardToken))
            throw new DomainRuleException("Autorize um cartão de crédito para iniciar o trial.");
        string trialPlan;
        try
        {
            trialPlan = await gateway.VerifyTrialPlanAsync(plan.Code, plan.MonthlyPrice, cancellationToken);
        }
        catch (RecurringCheckoutGatewayException)
        {
            throw new DomainRuleException("O plano de trial não está disponível para autorização.");
        }
        Subscription attempt;
        bool dispatch;
        try
        {
            (attempt, dispatch) = await subscriptions.ExecuteSerializableAsync(async token =>
            {
                var existing = await subscriptions.FindCheckoutAsync(tenantId, key, token);
                if (existing is not null)
                {
                    CheckReplayPlan(existing, plan);
                    if (existing.CheckoutDispatchedAt.HasValue || existing.GatewaySubscriptionId is not null
                        || existing.Status != SubscriptionStatus.PendingCheckout)
                    {
                        return (existing, false);
                    }
                }
                else if (await subscriptions.HasPendingCheckoutAsync(tenantId, token))
                {
                    throw new ConcurrencyConflictException("Já existe uma contratação pendente para o estabelecimento.");
                }

                var current = await subscriptions.FindCurrentAsync(tenantId, token);
                if (current?.Status != SubscriptionStatus.AwaitingTrialAuthorization)
                {
                    throw new DomainRuleException("O estabelecimento já possui assinatura paga vigente.");
                }
                var count = await professionals.CountEligibleAsync(tenantId, token);
                if (!plan.IsActive || !plan.AllowsProfessionals(count) || plan.MonthlyPrice.Amount <= 0)
                {
                    throw new DomainRuleException("O plano está indisponível ou não comporta a equipe elegível.");
                }

                var now = clock.GetUtcNow();
                var today = UtcDay(now);
                var checkout = existing ?? Subscription.StartCheckout(tenantId, plan, key, today);
                if (existing is null)
                {
                    checkout.UseAuthorizedTrial(trialPlan, current.PlanId);
                    await subscriptions.AddAsync(checkout, token);
                }

                checkout.MarkCheckoutDispatched(now, user.Email);
                await subscriptions.SaveChangesAsync(token);
                return (checkout, true);
            }, cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            var replay = await subscriptions.FindCheckoutAsync(tenantId, key, cancellationToken, readOnly: true);
            if (replay is null)
            {
                throw;
            }
            CheckReplayPlan(replay, plan);
            return await DescribeAsync(replay, cancellationToken);
        }

        if (!dispatch)
        {
            return await DescribeAsync(attempt, cancellationToken);
        }

        // O marcador foi confirmado antes do POST. Se a resposta se perder
        // ou o processo cair, só uma verificação poderá resolver a tentativa.
        RecurringAuthorization authorization;
        try
        {
            authorization = await gateway.CreateAsync(new RecurringCheckoutRequest(
                key, Reference(attempt), user.Email, Amount(attempt), attempt.BillingIntervalMonths!.Value,
                attempt.GatewayTrialPlanId, cardToken), cancellationToken);
        }
        catch (RecurringCheckoutGatewayException error)
        {
            if (error.Rejected)
            {
                return await PersistGatewayResultAsync(attempt, checkout => checkout.FailCheckout(UtcDay(clock.GetUtcNow())),
                    cancellationToken);
            }
            return await DescribeAsync(attempt, cancellationToken);
        }

        if (authorization.Id is { } id && Matches(attempt, authorization))
        {
            return await PersistGatewayResultAsync(attempt,
                checkout => checkout.BindCheckout(id),
                cancellationToken);
        }
        return await DescribeAsync(attempt, cancellationToken);
    }

    private async Task<CheckoutResult> PersistGatewayResultAsync(Subscription attempt, Action<Subscription> update,
        CancellationToken cancellationToken)
    {
        try
        {
            await subscriptions.ExecuteSerializableAsync(async token =>
            {
                var persisted = await LoadAsync(attempt.TenantId, attempt.CheckoutKey!.Value, token, readOnly: true);
                if (persisted.Status != SubscriptionStatus.PendingCheckout || persisted.GatewaySubscriptionId is not null)
                {
                    return false;
                }
                update(attempt);
                await subscriptions.SaveChangesAsync(token);
                return true;
            }, cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return await GetAsync(attempt.TenantId, attempt.CheckoutKey!.Value, cancellationToken);
        }
        return await GetAsync(attempt.TenantId, attempt.CheckoutKey!.Value, cancellationToken);
    }

    public Task<bool> BindRecoveredAsync(Guid tenantId, Guid key, RecurringAuthorization authorization,
        CancellationToken cancellationToken) =>
        subscriptions.ExecuteSerializableAsync(async token =>
        {
            var attempt = await LoadAsync(tenantId, key, token);
            if (!Matches(attempt, authorization) || authorization.Id is not { } id)
            {
                return false;
            }
            if (attempt.GatewaySubscriptionId is not null)
            {
                return attempt.GatewaySubscriptionId == id;
            }
            if (attempt.Status != SubscriptionStatus.PendingCheckout
                || attempt.CheckoutDispatchedAt is not { } dispatched
                || authorization.CreatedAt is not { } created || created < dispatched || created > clock.GetUtcNow())
            {
                return false;
            }
            attempt.BindCheckout(id);
            await subscriptions.SaveChangesAsync(token);
            return true;
        }, cancellationToken);

    public async Task<CheckoutResult> GetAsync(Guid tenantId, Guid key, CancellationToken cancellationToken) =>
        await DescribeAsync(await LoadAsync(tenantId, key, cancellationToken, readOnly: true), cancellationToken);

    public async Task<CheckoutResult> VerifyAsync(Guid tenantId, Guid? userId, Guid key, CancellationToken cancellationToken,
        RecurringAuthorization? observedAuthorization = null)
    {
        var attempt = await LoadAsync(tenantId, key, cancellationToken, readOnly: true);
        if (attempt.Status is SubscriptionStatus.Active or SubscriptionStatus.Cancelled or SubscriptionStatus.Expired
            || attempt.GatewaySubscriptionId is not { } externalId)
        {
            return await DescribeAsync(attempt, cancellationToken);
        }

        var gateway = RequireGateway();
        RecurringAuthorization authorization;
        try
        {
            authorization = observedAuthorization ?? await gateway.FindAsync(externalId, cancellationToken);
        }
        catch (RecurringCheckoutGatewayException)
        {
            return new CheckoutResult(attempt, "RequiresReconciliation");
        }
        if (authorization.Id != externalId || !Matches(attempt, authorization)
            || authorization.Status is not ("pending" or "authorized" or "cancelled" or "canceled" or "paused"))
        {
            return new CheckoutResult(attempt, "RequiresReconciliation");
        }

        var observedAt = clock.GetUtcNow();
        var startingTrial = attempt.GatewayTrialPlanId is not null && attempt.TrialStartedAtUtc is null;
        var cancellationObserved = authorization.Status is "cancelled" or "canceled";
        if (attempt.CancellationRequestedOn.HasValue && !cancellationObserved)
        {
            return new CheckoutResult(attempt, "RequiresReconciliation");
        }
        if (authorization.Status == "paused")
        {
            return new CheckoutResult(attempt, "AwaitingFinancialConfirmation");
        }
        if (attempt.IsOnTrial && attempt.TrialDeadline > observedAt)
        {
            return authorization.Status == "authorized"
                ? await DescribeAsync(attempt, cancellationToken)
                : new CheckoutResult(attempt, "RequiresReconciliation");
        }

        RecurringCycleEvidence evidence = new();
        var paymentLookupFailed = false;
        var originalTrialDue = authorization.CreatedAt?.AddDays(Subscription.TrialDays);
        if (cancellationObserved && startingTrial && !originalTrialDue.HasValue)
        {
            return new CheckoutResult(attempt, "CancellationPending");
        }
        var firstCycleDue = startingTrial ? originalTrialDue : attempt.TrialEndsAtUtc;
        var checkFirstCycle = (!startingTrial || originalTrialDue <= observedAt)
            && (authorization.Status == "authorized" || cancellationObserved);
        if (checkFirstCycle)
        {
            if (firstCycleDue is not { } deadline)
            {
                return new CheckoutResult(attempt, "RequiresReconciliation");
            }
            try
            {
                evidence = await gateway.FindCyclePaymentAsync(externalId, deadline, Amount(attempt), cancellationToken);
            }
            catch (RecurringCheckoutGatewayException) when (observedAuthorization is null)
            {
                paymentLookupFailed = true;
            }
        }
        if (cancellationObserved && checkFirstCycle && evidence.Payment is null && !evidence.NoInvoice && !evidence.Rejected)
        {
            return new CheckoutResult(attempt, "CancellationPending");
        }

        try
        {
            return await subscriptions.ExecuteSerializableAsync(async token =>
            {
                var stored = await LoadAsync(tenantId, key, token);
                if (stored.Status is SubscriptionStatus.Active or SubscriptionStatus.Cancelled or SubscriptionStatus.Expired)
                {
                    return await DescribeAsync(stored, token);
                }
                if (stored.CancellationRequestedOn.HasValue && !cancellationObserved)
                {
                    return new CheckoutResult(stored, "RequiresReconciliation");
                }
                if (stored.IsOnTrial)
                {
                    if (stored.TrialDeadline > clock.GetUtcNow())
                    {
                        return await DescribeAsync(stored, token);
                    }
                    stored.ExpireTrial(clock.GetUtcNow());
                }
                if (cancellationObserved && evidence.Payment is null)
                {
                    if (stored.TrialStartedAtUtc.HasValue)
                    {
                        if (evidence.Rejected && firstCycleDue is { } due)
                        {
                            (await GetInvoiceAsync(stored, due, token)).RecordRecurringFailure(due);
                        }
                        stored.RequestCancellation(UtcDay(observedAt));
                        stored.ConfirmCancellation(externalId, observedAt);
                    }
                    else
                    {
                        stored.FailCheckout(UtcDay(observedAt));
                    }
                }
                else if (authorization.Status == "pending")
                {
                    if (stored.Status != SubscriptionStatus.PendingCheckout)
                    {
                        return new CheckoutResult(stored, "RequiresReconciliation");
                    }
                    stored.BindCheckout(externalId);
                }
                else
                {
                    if (startingTrial && stored.TrialStartedAtUtc is null)
                    {
                        if (authorization.CreatedAt is not { } authorizedAt || originalTrialDue is not { } due
                            || authorizedAt > observedAt || authorizedAt < stored.CheckoutDispatchedAt
                            || (authorization.NextChargeAt != due
                                && (observedAt < due || authorization.NextChargeAt <= due
                                    || !authorization.NextChargeAt.HasValue || evidence.DueAt != due)))
                        {
                            return new CheckoutResult(stored, "RequiresReconciliation");
                        }
                        var current = await subscriptions.FindCurrentAsync(tenantId, token);
                        if (current?.Status != SubscriptionStatus.AwaitingTrialAuthorization)
                        {
                            return new CheckoutResult(stored, "RequiresReconciliation");
                        }
                        stored.ConfirmAuthorization(externalId);
                        stored.BeginAuthorizedTrial(externalId, authorizedAt, due);
                        if (observedAt >= due)
                        {
                            stored.ExpireTrial(observedAt);
                        }
                        else
                        {
                            await subscriptions.SaveChangesAsync(token);
                            return await DescribeAsync(stored, token);
                        }
                    }
                    if (stored.Status != SubscriptionStatus.AuthorizedAwaitingFirstPayment)
                    {
                        stored.ConfirmAuthorization(externalId);
                    }
                    if (evidence.Payment is { } payment)
                    {
                        var issue = await TryActivateAsync(stored, payment, userId, token);
                        if (issue is not null)
                        {
                            await subscriptions.SaveChangesAsync(token);
                            return new CheckoutResult(stored, issue);
                        }
                        if (cancellationObserved)
                        {
                            stored.RequestCancellation(UtcDay(observedAt));
                            if (stored.ChargeDeadline > observedAt)
                            {
                                stored.ConfirmCancellation(externalId, observedAt);
                                await subscriptions.SaveChangesAsync(token);
                                return new CheckoutResult(stored, "CancellationScheduled");
                            }
                        }
                    }
                    else if ((stored.TrialEndsAtUtc ?? evidence.DueAt) is { } deadline
                        && deadline <= observedAt && stored.CheckoutDispatchedAt <= deadline)
                    {
                        stored.RecordFirstPaymentOutstanding(deadline);
                        var invoice = await GetInvoiceAsync(stored, deadline, token);
                        if (evidence.Rejected) invoice.RecordRecurringFailure(deadline);
                    }
                }
                await subscriptions.SaveChangesAsync(token);
                return paymentLookupFailed
                    ? new CheckoutResult(stored, "RequiresReconciliation")
                    : evidence.Rejected ? new CheckoutResult(stored, "FirstPaymentRejected")
                    : await DescribeAsync(stored, token);
            }, cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            var latest = await LoadAsync(tenantId, key, cancellationToken, readOnly: true);
            if (latest.Status is SubscriptionStatus.Active or SubscriptionStatus.Cancelled)
            {
                return await DescribeAsync(latest, cancellationToken);
            }
            throw;
        }
    }

    private async Task<string?> TryActivateAsync(Subscription attempt, ApprovedRecurringPayment payment,
        Guid? userId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var today = UtcDay(now);
        var start = UtcDay(payment.DueAt);
        var end = start.AddMonths(attempt.BillingIntervalMonths!.Value);
        if (attempt.CheckoutDispatchedAt is not { } dispatched || start < UtcDay(dispatched)
            || payment.ApprovedAt < dispatched || payment.ApprovedAt > now || today < start)
        {
            return "RequiresReconciliation";
        }
        if (attempt.TrialStartedAtUtc is null || attempt.TrialEndsAtUtc is not { } trialEnd
            || payment.DueAt != trialEnd || payment.ApprovedAt < trialEnd)
        {
            return "RequiresReconciliation";
        }

        var current = await subscriptions.FindCurrentAsync(attempt.TenantId, cancellationToken);
        if (IsPaidCurrent(current) || TrialEndStillAhead(current, now).HasValue)
        {
            return "RequiresReconciliation";
        }
        var plan = await plans.FindByIdAsync(attempt.PlanId, cancellationToken);
        var count = await professionals.CountEligibleAsync(attempt.TenantId, cancellationToken);
        if (plan is not { IsActive: true } || !plan.AllowsProfessionals(count))
        {
            return "CapacityExceeded";
        }
        var history = PlanChange.Record(attempt.TenantId, attempt.Id, null, attempt.PlanId,
            PlanChangeReason.Initial, userId, now);
        if (await subscriptions.IsRecurringPaymentUsedAsync(payment.Id, cancellationToken))
        {
            throw new ConcurrencyConflictException("O pagamento já está associado a uma assinatura.");
        }

        if (current?.IsOnTrial == true)
        {
            current.ExpireTrial(today);
            // O índice parcial é imediato. Libera a vigência anterior primeiro,
            // dentro da mesma transação que grava o primeiro pagamento e o histórico.
            await subscriptions.SaveChangesAsync(cancellationToken);
        }
        attempt.ConfirmFirstPayment(attempt.GatewaySubscriptionId!, payment.Id, plan, count, start, end);
        attempt.SetFirstCycleDeadline(payment.DueAt.ToUniversalTime().AddMonths(attempt.BillingIntervalMonths!.Value));
        var invoice = await GetInvoiceAsync(attempt, payment.DueAt, cancellationToken);
        invoice.MarkPaid(payment.Id, payment.ApprovedAt.ToUniversalTime());
        await subscriptions.AddChangeAsync(history, cancellationToken);
        return null;
    }

    private async Task<PlatformInvoice> GetInvoiceAsync(Subscription attempt, DateTimeOffset due,
        CancellationToken cancellationToken)
    {
        var invoice = await subscriptions.FindRecurringInvoiceAsync(attempt.TenantId, attempt.Id, due, cancellationToken);
        if (invoice is null)
        {
            invoice = PlatformInvoice.CreateRecurring(attempt.TenantId, attempt.Id, due,
                due.ToUniversalTime().AddMonths(attempt.BillingIntervalMonths!.Value), Amount(attempt));
            await subscriptions.AddRecurringInvoiceAsync(invoice, cancellationToken);
        }
        return invoice;
    }

    private async Task<CheckoutResult> DescribeAsync(Subscription attempt, CancellationToken cancellationToken)
    {
        if (attempt.Status == SubscriptionStatus.PendingCheckout && attempt.CheckoutDispatchedAt is null)
        {
            return new CheckoutResult(attempt, "RequiresReconciliation");
        }
        var state = attempt.Status switch
        {
            SubscriptionStatus.Active => "Active",
            SubscriptionStatus.Trialing => "Trialing",
            SubscriptionStatus.Cancelled or SubscriptionStatus.Expired => "Failed",
            SubscriptionStatus.AuthorizedAwaitingFirstPayment => "AwaitingFinancialConfirmation",
            _ when attempt.GatewaySubscriptionId is not null => "AwaitingExternalConfirmation",
            _ => "RequiresReconciliation"
        };
        return new CheckoutResult(attempt, state);
    }

    private async Task<Subscription> LoadAsync(Guid tenantId, Guid key, CancellationToken cancellationToken, bool readOnly = false) =>
        await subscriptions.FindCheckoutAsync(tenantId, key, cancellationToken, readOnly)
            ?? throw NotFoundException.For("Tentativa de contratação", key);

    private IRecurringCheckoutGateway RequireGateway() =>
        _gateway ?? throw new InvalidOperationException("A contratação recorrente está indisponível.");

    private static DateOnly UtcDay(DateTimeOffset now) => DateOnly.FromDateTime(now.UtcDateTime);
    private static DateOnly? TrialEndStillAhead(Subscription? current, DateTimeOffset now) =>
        current is { FirstPaymentId: null, TrialDeadline: { } end } && now < end ? UtcDay(end) : null;
    private static bool IsPaidCurrent(Subscription? current) => current is { Status: SubscriptionStatus.Active }
        or { Status: SubscriptionStatus.PastDue, TrialEndsAt: null };
    private static string Reference(Subscription attempt) => attempt.Id.ToString("N");
    private static Money Amount(Subscription attempt) => Money.From(attempt.ContractPriceAmount!.Value, attempt.ContractCurrency!);

    private static bool Matches(Subscription attempt, RecurringAuthorization authorization) =>
        authorization.ExternalReference == Reference(attempt)
        && authorization.Amount == attempt.ContractPriceAmount
        && authorization.Currency == attempt.ContractCurrency
        && authorization.Frequency == attempt.BillingIntervalMonths
        && authorization.FrequencyType == "months"
        && authorization.TrialPlanId == attempt.GatewayTrialPlanId;

    private static void CheckReplayPlan(Subscription attempt, Plan plan)
    {
        if (attempt.PlanId != plan.Id)
        {
            throw new ConcurrencyConflictException("A chave já pertence a uma tentativa para outro plano.");
        }
    }
}

public sealed record CheckoutResult(Subscription Attempt, string State);
