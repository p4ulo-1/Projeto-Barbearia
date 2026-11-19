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

    public async Task<CheckoutResult> StartAsync(Guid tenantId, Guid userId, Guid key, string planCode,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || key == Guid.Empty)
        {
            throw new DomainRuleException("Estabelecimento e chave da contratação são obrigatórios.");
        }

        var gateway = RequireGateway();
        var user = await users.FindByIdAsync(tenantId, userId, cancellationToken);
        if (user is not { IsActive: true })
        {
            throw NotFoundException.For("Usuário", userId);
        }

        var plan = await plans.FindByCodeAsync(planCode, cancellationToken)
            ?? throw NotFoundException.For("Plano", planCode);
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
                if (IsPaidCurrent(current))
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
                    await subscriptions.AddAsync(checkout, token);
                }

                // O início futuro do preapproval ainda precisa ser homologado. O trial
                // conserva sua validade original e nenhuma cobrança pode começar nele.
                var mayDispatch = TrialEndStillAhead(current, today) is null;
                if (mayDispatch)
                {
                    checkout.MarkCheckoutDispatched(now);
                }
                await subscriptions.SaveChangesAsync(token);
                return (checkout, mayDispatch);
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
                key, Reference(attempt), user.Email, Amount(attempt), attempt.BillingIntervalMonths!.Value), cancellationToken);
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
                checkout => checkout.BindCheckout(id, authorization.Status == "pending" ? authorization.CheckoutUrl : null),
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
                update(attempt);
                await subscriptions.SaveChangesAsync(token);
                return true;
            }, cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return await GetAsync(attempt.TenantId, attempt.CheckoutKey!.Value, cancellationToken);
        }
        return await DescribeAsync(attempt, cancellationToken);
    }

    public async Task<CheckoutResult> GetAsync(Guid tenantId, Guid key, CancellationToken cancellationToken) =>
        await DescribeAsync(await LoadAsync(tenantId, key, cancellationToken, readOnly: true), cancellationToken);

    public async Task<CheckoutResult> VerifyAsync(Guid tenantId, Guid userId, Guid key, CancellationToken cancellationToken)
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
            authorization = await gateway.FindAsync(externalId, cancellationToken);
        }
        catch (RecurringCheckoutGatewayException)
        {
            return new CheckoutResult(attempt, "RequiresReconciliation");
        }
        if (authorization.Id != externalId || !Matches(attempt, authorization)
            || authorization.Status is not ("pending" or "authorized" or "cancelled" or "canceled"))
        {
            return new CheckoutResult(attempt, "RequiresReconciliation");
        }

        ApprovedRecurringPayment? payment = null;
        var paymentLookupFailed = false;
        if (authorization.Status == "authorized")
        {
            try
            {
                payment = await gateway.FindFirstApprovedPaymentAsync(externalId, Amount(attempt), cancellationToken);
            }
            catch (RecurringCheckoutGatewayException)
            {
                paymentLookupFailed = true;
            }
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
                if (authorization.Status is "cancelled" or "canceled")
                {
                    stored.FailCheckout(UtcDay(clock.GetUtcNow()));
                }
                else if (authorization.Status == "pending")
                {
                    if (stored.Status != SubscriptionStatus.PendingCheckout)
                    {
                        return new CheckoutResult(stored, "RequiresReconciliation");
                    }
                    stored.BindCheckout(externalId, authorization.CheckoutUrl);
                }
                else
                {
                    if (stored.Status != SubscriptionStatus.AuthorizedAwaitingFirstPayment)
                    {
                        stored.ConfirmAuthorization(externalId);
                    }
                    if (payment is not null)
                    {
                        var issue = await TryActivateAsync(stored, payment, userId, token);
                        if (issue is not null)
                        {
                            await subscriptions.SaveChangesAsync(token);
                            return new CheckoutResult(stored, issue);
                        }
                    }
                }
                await subscriptions.SaveChangesAsync(token);
                return paymentLookupFailed
                    ? new CheckoutResult(stored, "RequiresReconciliation")
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
        Guid userId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var today = UtcDay(now);
        var start = UtcDay(payment.DueAt);
        var end = start.AddMonths(attempt.BillingIntervalMonths!.Value);
        if (attempt.CheckoutDispatchedAt is not { } dispatched || start < UtcDay(dispatched)
            || payment.ApprovedAt < dispatched || payment.ApprovedAt > now || today < start || today >= end)
        {
            return "RequiresReconciliation";
        }

        var current = await subscriptions.FindCurrentAsync(attempt.TenantId, cancellationToken);
        if (IsPaidCurrent(current) || TrialEndStillAhead(current, today).HasValue)
        {
            return "RequiresReconciliation";
        }
        var plan = await plans.FindByIdAsync(attempt.PlanId, cancellationToken);
        var count = await professionals.CountEligibleAsync(attempt.TenantId, cancellationToken);
        if (plan is null || !plan.AllowsProfessionals(count))
        {
            return "CapacityExceeded";
        }
        var history = PlanChange.Record(attempt.TenantId, attempt.Id, null, attempt.PlanId,
            PlanChangeReason.Initial, userId, now);

        if (current?.IsOnTrial == true)
        {
            current.ExpireTrial(today);
            // O índice parcial é imediato. Libera a vigência anterior primeiro,
            // dentro da mesma transação que grava o primeiro pagamento e o histórico.
            await subscriptions.SaveChangesAsync(cancellationToken);
        }
        attempt.ConfirmFirstPayment(attempt.GatewaySubscriptionId!, payment.Id, plan, count, start, end);
        await subscriptions.AddChangeAsync(history, cancellationToken);
        return null;
    }

    private async Task<CheckoutResult> DescribeAsync(Subscription attempt, CancellationToken cancellationToken)
    {
        if (attempt.Status == SubscriptionStatus.PendingCheckout && attempt.CheckoutDispatchedAt is null)
        {
            var current = await subscriptions.FindCurrentAsync(attempt.TenantId, cancellationToken);
            var end = TrialEndStillAhead(current, UtcDay(clock.GetUtcNow()));
            return new CheckoutResult(attempt, end.HasValue ? "WaitingForTrialEnd" : "ReadyToResume", end);
        }
        var state = attempt.Status switch
        {
            SubscriptionStatus.Active => "Active",
            SubscriptionStatus.Cancelled or SubscriptionStatus.Expired => "Failed",
            SubscriptionStatus.AuthorizedAwaitingFirstPayment => "AwaitingFinancialConfirmation",
            _ when attempt.CheckoutUrl is not null => "AwaitingAuthorization",
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
    private static DateOnly? TrialEndStillAhead(Subscription? current, DateOnly today) =>
        current is { IsOnTrial: true, TrialEndsAt: { } end } && today < end ? end : null;
    private static bool IsPaidCurrent(Subscription? current) => current is { Status: SubscriptionStatus.Active }
        or { Status: SubscriptionStatus.PastDue, TrialEndsAt: null };
    private static string Reference(Subscription attempt) => attempt.Id.ToString("N");
    private static Money Amount(Subscription attempt) => Money.From(attempt.ContractPriceAmount!.Value, attempt.ContractCurrency!);

    private static bool Matches(Subscription attempt, RecurringAuthorization authorization) =>
        authorization.ExternalReference == Reference(attempt)
        && authorization.Amount == attempt.ContractPriceAmount
        && authorization.Currency == attempt.ContractCurrency
        && authorization.Frequency == attempt.BillingIntervalMonths
        && authorization.FrequencyType == "months";

    private static void CheckReplayPlan(Subscription attempt, Plan plan)
    {
        if (attempt.PlanId != plan.Id)
        {
            throw new ConcurrencyConflictException("A chave já pertence a uma tentativa para outro plano.");
        }
    }
}

public sealed record CheckoutResult(Subscription Attempt, string State, DateOnly? ResumeOn = null);
