using Servia.Domain.Billing;
using Servia.Domain.Common;
using Servia.Domain.Identity;
using Servia.Domain.Tenancy;

namespace Servia.Application.Billing;

public sealed class SubscriptionPlanChangeService(ISubscriptionRepository repository, IPlanRepository plans,
    IUserRepository users, IProfessionalCountReader professionals, IEnumerable<IRecurringCheckoutGateway> gateways,
    TimeProvider clock)
{
    private readonly IRecurringCheckoutGateway? _gateway = gateways.SingleOrDefault();
    private static readonly TimeSpan DowngradeLead = TimeSpan.FromHours(48);
    private static readonly TimeSpan DispatchMargin = TimeSpan.FromMinutes(15);

    public async Task<PlanChange> RequestAsync(Guid tenantId, Guid userId, string targetCode, Guid operationKey,
        CancellationToken cancellationToken)
    {
        if (operationKey == Guid.Empty) throw new DomainRuleException("Identificador da operação inválido.");
        var change = await repository.ExecuteSerializableAsync(async token =>
        {
            var subscription = await CurrentAsync(tenantId, token);
            var target = await plans.FindByCodeAsync(targetCode, token) ?? throw NotFoundException.For("Plano", targetCode);
            var items = await repository.ListChangesAsync(tenantId, token);
            var replay = items.FirstOrDefault(item => item.OperationKey == operationKey);
            if (replay is not null)
            {
                if (replay.ToPlanId != target.Id) throw new ConcurrencyConflictException("A chave já pertence a outro plano.");
                return replay;
            }
            var pending = items.FirstOrDefault(item => item.SubscriptionId == subscription.Id && item.Status == PlanChangeStatus.Requested);
            var now = clock.GetUtcNow();
            if (pending is not null)
            {
                if (ExpiredQuote(pending, now))
                {
                    pending.Fail("QuoteExpired");
                    await repository.SaveChangesAsync(token);
                }
                else if (pending.ToPlanId == target.Id) return pending;
                else throw new ConcurrencyConflictException("Já existe uma alteração de plano pendente.");
            }
            if (_gateway is null || !CanChange(subscription, now))
                throw new DomainRuleException("A assinatura não permite solicitar alteração neste momento.");
            var source = await plans.FindByIdAsync(subscription.EffectivePlanId, token)
                ?? throw NotFoundException.For("Plano", subscription.EffectivePlanId);
            var start = subscription.IsActive
                ? await repository.FindPaidCycleStartAsync(tenantId, subscription.Id, subscription.CurrentPeriodStart, token)
                : subscription.TrialStartedAtUtc;
            var count = await professionals.CountEligibleAsync(tenantId, token);
            var created = PlanChange.Request(subscription, source, target, count, userId, now, operationKey, start);
            await repository.AddChangeAsync(created, token);
            await repository.SaveChangesAsync(token);
            return created;
        }, cancellationToken);
        if (change.Status == PlanChangeStatus.Requested && change.AdditionalAmount is not > 0)
            await SynchronizeAsync(tenantId, change.SubscriptionId, cancellationToken);
        return await GetAsync(tenantId, change.Id, cancellationToken);
    }

    public async Task<PlanChange> ConfirmPaymentAsync(Guid tenantId, Guid userId, Guid changeId,
        string cardToken, string method, CancellationToken cancellationToken)
    {
        var gateway = _gateway ?? throw new DomainRuleException("Pagamento indisponível.");
        var change = await GetAsync(tenantId, changeId, cancellationToken);
        if (change.Status != PlanChangeStatus.Requested || change.AdditionalAmount is not > 0)
            return change;
        if (!change.PaymentDispatchedAt.HasValue)
        {
            var user = await users.FindByIdAsync(tenantId, userId, cancellationToken);
            if (user is not { IsActive: true } || !await gateway.IsCreditCardMethodAsync(method, cancellationToken))
                throw new DomainRuleException("Informe um cartão de crédito para autorizar a cobrança.");
            var subscription = await CurrentAsync(tenantId, cancellationToken, readOnly: true);
            if (!MatchesCycle(subscription, change, clock.GetUtcNow()) || ExpiredQuote(change, clock.GetUtcNow()))
                throw new DomainRuleException("A cotação expirou. Solicite uma nova cotação.");
            var authorization = await gateway.FindAsync(subscription.GatewaySubscriptionId!, cancellationToken);
            if (await ExternalIssueAsync(subscription, change, authorization, gateway, cancellationToken) is not null)
                throw new DomainRuleException("O provedor não confirmou as condições do ciclo para este upgrade.");
            var dispatch = await repository.ExecuteSerializableAsync(async token =>
            {
                var stored = await GetAsync(tenantId, changeId, token);
                var current = await CurrentAsync(tenantId, token);
                var now = clock.GetUtcNow();
                if (stored.PaymentDispatchedAt.HasValue || stored.Status != PlanChangeStatus.Requested) return false;
                if (ExpiredQuote(stored, now) || !MatchesCycle(current, stored, now))
                    throw new DomainRuleException("A cotação expirou. Solicite uma nova cotação.");
                stored.DispatchPayment(now);
                await repository.SaveChangesAsync(token);
                return true;
            }, cancellationToken);
            if (dispatch)
            {
                try
                {
                    await gateway.CreateUpgradePaymentAsync(changeId, Money.From(change.AdditionalAmount.Value,
                        change.RequestedCurrency!), cardToken, method, user.Email, cancellationToken);
                }
                catch (RecurringCheckoutGatewayException)
                {
                    // O marcador impede um novo débito. A busca usa a referência da operação.
                }
            }
        }
        await SynchronizeAsync(tenantId, change.SubscriptionId, cancellationToken);
        return await GetAsync(tenantId, changeId, cancellationToken);
    }

    public async Task<bool> SynchronizeAsync(Guid tenantId, Guid subscriptionId, CancellationToken cancellationToken,
        bool propagateGatewayError = false)
    {
        var item = (await repository.ListChangesAsync(tenantId, cancellationToken)).FirstOrDefault(change =>
            change.SubscriptionId == subscriptionId && change.Status == PlanChangeStatus.Requested && change.OperationKey != null);
        if (item is null || _gateway is not { } gateway) return false;
        try
        {
            if (item.AdditionalAmount is > 0 && !item.PaymentApprovedAt.HasValue)
            {
                if (!item.PaymentDispatchedAt.HasValue)
                {
                    if (ExpiredQuote(item, clock.GetUtcNow())) await FlagAsync(tenantId, item.Id, "QuoteExpired", true, cancellationToken);
                    return true;
                }
                var evidence = await gateway.FindUpgradePaymentAsync(item.Id, cancellationToken);
                if (evidence is null) return true;
                var paymentReady = await StorePaymentAsync(tenantId, item.Id, evidence, cancellationToken);
                if (!paymentReady) return true;
            }
            var subscription = await CurrentAsync(tenantId, cancellationToken, readOnly: true);
            var now = clock.GetUtcNow();
            if (item.Reason == PlanChangeReason.Downgrade && !item.HasExternalEffects && item.CycleEnd - now > DowngradeLead)
                return false;
            if (!MatchesCycle(subscription, item, now))
            {
                if (item.Reason == PlanChangeReason.Downgrade && item.RecurrenceConfirmedAt.HasValue &&
                    subscription.Id == item.SubscriptionId && subscription.ChargeDeadline == item.CycleEnd)
                    return false; // A renovação valida e aplica a alteração no vencimento.
                await FlagAsync(tenantId, item.Id, "CycleChanged", !item.HasExternalEffects, cancellationToken);
                return true;
            }
            var authorization = await gateway.FindAsync(subscription.GatewaySubscriptionId!, cancellationToken);
            var externalIssue = await ExternalIssueAsync(subscription, item, authorization, gateway, cancellationToken);
            if (externalIssue is not null)
            {
                await FlagAsync(tenantId, item.Id, externalIssue, false, cancellationToken);
                return true;
            }
            if (authorization.Amount != item.RequestedPrice)
            {
                var dispatch = await ClaimRecurrenceAsync(tenantId, item.Id, cancellationToken);
                if (!dispatch) return true;
                try
                {
                    await gateway.UpdateRecurringPriceAsync(authorization.Id!, TargetAmount(item), cancellationToken);
                }
                catch (RecurringCheckoutGatewayException) when (!propagateGatewayError)
                {
                    return true;
                }
                authorization = await gateway.FindAsync(authorization.Id!, cancellationToken);
            }
            if (authorization.Amount == item.RequestedPrice &&
                await ExternalIssueAsync(subscription, item, authorization, gateway, cancellationToken) is null)
                await ApplyRecurrenceAsync(tenantId, item.Id, cancellationToken);
            return item.Status == PlanChangeStatus.Requested && !item.RecurrenceConfirmedAt.HasValue;
        }
        catch (RecurringCheckoutGatewayException) when (!propagateGatewayError)
        {
            return true;
        }
    }

    private Task<bool> StorePaymentAsync(Guid tenantId, Guid id, UpgradePaymentEvidence evidence, CancellationToken ct) =>
        repository.ExecuteSerializableAsync(async token =>
        {
            var item = await GetAsync(tenantId, id, token);
            if (item.PaymentApprovedAt.HasValue) return true;
            if (item.Status != PlanChangeStatus.Requested || item.PaymentDispatchedAt is null) return false;
            if (evidence.Amount != item.AdditionalAmount || evidence.Currency != item.RequestedCurrency ||
                evidence.PaymentType != "credit_card")
                item.Flag("PaymentMismatch");
            else if (evidence.Status is "rejected" or "cancelled") item.Fail("PaymentRejected");
            else if (evidence is { Status: "approved", Refunded: 0m, ApprovedAt: { } approved }
                && approved >= item.PaymentDispatchedAt && approved <= clock.GetUtcNow()
                && !await repository.IsRecurringPaymentUsedAsync(evidence.Id, token))
                item.ApprovePayment(evidence.Id, approved.ToUniversalTime());
            await repository.SaveChangesAsync(token);
            return item.PaymentApprovedAt.HasValue;
        }, ct);

    private Task<bool> ClaimRecurrenceAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        repository.ExecuteSerializableAsync(async token =>
        {
            var item = await GetAsync(tenantId, id, token);
            var current = await CurrentAsync(tenantId, token);
            var now = clock.GetUtcNow();
            if (item.RecurrenceDispatchedAt.HasValue || !MatchesCycle(current, item, now)
                || item.CycleEnd - now <= DispatchMargin) return false;
            if (!await HasCapacityAsync(current, item, token))
            {
                if (item.HasExternalEffects) item.Flag("CapacityExceeded"); else item.Fail("CapacityExceeded");
                await repository.SaveChangesAsync(token);
                return false;
            }
            item.DispatchRecurrence(now);
            await repository.SaveChangesAsync(token);
            return true;
        }, ct);

    private Task ApplyRecurrenceAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        repository.ExecuteSerializableAsync(async token =>
        {
            var item = await GetAsync(tenantId, id, token);
            var current = await CurrentAsync(tenantId, token);
            var now = clock.GetUtcNow();
            if (!MatchesCycle(current, item, now)) return false;
            var target = await plans.FindByIdAsync(item.ToPlanId, token);
            var count = await professionals.CountEligibleAsync(tenantId, token);
            if (target is not { IsActive: true } || !target.AllowsProfessionals(count))
            {
                item.Flag("CapacityExceeded");
            }
            else if (item.Reason == PlanChangeReason.Downgrade) item.ConfirmRecurrence(now);
            else if (item.AdditionalAmount is not > 0 || item.PaymentApprovedAt.HasValue)
            {
                if (current.IsOnTrial) current.ApplyConfirmedTrialUpgrade(target, count, TargetAmount(item));
                else current.ApplyConfirmedPlan(target, count, TargetAmount(item));
                item.ConfirmUpgrade(now);
            }
            await repository.SaveChangesAsync(token);
            return true;
        }, ct);

    private async Task<bool> HasCapacityAsync(Subscription subscription, PlanChange change, CancellationToken ct) =>
        await plans.FindByIdAsync(change.ToPlanId, ct) is { IsActive: true } target
        && target.AllowsProfessionals(await professionals.CountEligibleAsync(subscription.TenantId, ct));

    private static async Task<string?> ExternalIssueAsync(Subscription subscription, PlanChange change,
        RecurringAuthorization remote, IRecurringCheckoutGateway gateway, CancellationToken ct)
    {
        if (remote is not { Status: "authorized" } || remote.Id != subscription.GatewaySubscriptionId
            || remote.ExternalReference != subscription.Id.ToString("N"))
            return "RecurringAuthorizationUnconfirmed";
        if (remote.NextChargeAt != change.CycleEnd) return "RecurringDateMismatch";
        if (remote.TrialPlanId != subscription.GatewayTrialPlanId || remote.Frequency != subscription.BillingIntervalMonths
            || remote.FrequencyType != "months" || remote.Currency != change.RequestedCurrency
            || (remote.Amount != change.SourcePrice && remote.Amount != change.RequestedPrice))
            return "RecurringTermsUnconfirmed";
        var due = await gateway.FindCyclePaymentAsync(remote.Id!, change.CycleEnd!.Value, TargetAmount(change), ct);
        return due.NoInvoice || (due.InvoiceAmount == change.RequestedPrice && due.Currency == change.RequestedCurrency &&
            due.Payment is null && !due.Rejected) ? null : "CycleInvoiceAlreadyIssued";
    }

    public async Task<PlanChange> GetAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        (await repository.ListChangesAsync(tenantId, ct)).FirstOrDefault(item => item.Id == id)
        ?? throw NotFoundException.For("Mudança de plano", id);

    public async Task<PlanChange?> GetLatestAsync(Guid tenantId, CancellationToken ct)
    {
        var current = await repository.FindCurrentAsync(tenantId, ct, readOnly: true);
        if (current is null) return null;
        return (await repository.ListChangesAsync(tenantId, ct)).FirstOrDefault(item =>
            item.SubscriptionId == current.Id && item.OperationKey.HasValue);
    }

    private Task FlagAsync(Guid tenantId, Guid id, string issue, bool terminal, CancellationToken ct) =>
        repository.ExecuteSerializableAsync(async token =>
        {
            var item = await GetAsync(tenantId, id, token);
            if (item.Status != PlanChangeStatus.Requested || item.Issue == issue) return false;
            if (terminal && !item.HasExternalEffects) item.Fail(issue); else item.Flag(issue);
            await repository.SaveChangesAsync(token);
            return true;
        }, ct);

    private async Task<Subscription> CurrentAsync(Guid tenant, CancellationToken ct, bool readOnly = false) =>
        await repository.FindCurrentAsync(tenant, ct, readOnly) ?? throw NotFoundException.For("Assinatura", tenant);

    private static bool CanChange(Subscription subscription, DateTimeOffset now) =>
        subscription.GatewaySubscriptionId is not null && subscription.CancellationRequestedOn is null
        && (subscription.IsOnTrial ? subscription.TrialDeadline > now
            : subscription.IsActive && subscription.FirstPaymentId is not null && subscription.ChargeDeadline > now);

    private static bool MatchesCycle(Subscription subscription, PlanChange change, DateTimeOffset now) =>
        change.Status == PlanChangeStatus.Requested && subscription.Id == change.SubscriptionId &&
        subscription.EffectivePlanId == change.FromPlanId && CanChange(subscription, now) &&
        (subscription.IsOnTrial ? subscription.TrialDeadline : subscription.ChargeDeadline) == change.CycleEnd;

    private static bool ExpiredQuote(PlanChange item, DateTimeOffset now) =>
        item.AdditionalAmount is > 0 && !item.HasExternalEffects && (item.ChangedAt.AddMinutes(15) <= now || item.CycleEnd - now <= DispatchMargin);

    private static Money TargetAmount(PlanChange item) => Money.From(item.RequestedPrice!.Value, item.RequestedCurrency!);
}
