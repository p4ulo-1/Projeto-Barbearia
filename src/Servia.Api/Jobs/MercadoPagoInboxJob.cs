using Servia.Application.Billing;
using Servia.Application.Jobs;
using Servia.Domain.Billing;
using Servia.Domain.Operations;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Repositories;

namespace Servia.Api.Jobs;

public sealed class MercadoPagoInboxJob(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    ILogger<MercadoPagoInboxJob> logger) : IScheduledJob
{
    public string Name => "mercadopago-inbox";
    public TimeSpan Interval => TimeSpan.FromSeconds(30);

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        for (var i = 0; i < 20 && await ProcessNextAsync(cancellationToken); i++) { }
    }

    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var inbox = services.GetRequiredService<MercadoPagoInbox>();
        var item = await inbox.ClaimAsync(clock.GetUtcNow(), cancellationToken);
        if (item is null) return false;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(90));
        var tenantContext = services.GetRequiredService<ITenantContext>();
        string result;
        TimeSpan? retryAfter = null;
        try
        {
            result = await ProcessAsync(item, inbox, services, tenantContext, timeout.Token);
        }
        catch (RecurringCheckoutGatewayException exception) when (exception.RateLimited)
        {
            result = "RateLimited";
            retryAfter = exception.RetryAfter > TimeSpan.Zero ? exception.RetryAfter : TimeSpan.FromMinutes(5);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // O lease permite recuperar a execução mesmo se o processo parar antes de gravar o resultado.
            result = "ProcessingFailed";
            logger.LogWarning("Falha ao processar notificação Mercado Pago {InboxId}, tentativa {Attempt}.",
                item.Id, item.Attempts);
        }
        finally
        {
            tenantContext.Clear();
        }

        var retry = result is "GatewayUnavailable" or "ProcessingFailed" or "UnlinkedResource" or "RateLimited"
            or "RequiresReconciliation" or "AwaitingCycle" or "UnconfirmedPayment" or "CapacityExceeded"
            or "CancellationPending";
        await inbox.FinishAsync(item, result, retry, clock.GetUtcNow(), cancellationToken, retryAfter);
        return true;
    }

    private async Task<string> ProcessAsync(MercadoPagoNotification item, MercadoPagoInbox inbox,
        IServiceProvider services, ITenantContext tenantContext, CancellationToken cancellationToken)
    {
        var gateway = services.GetServices<IRecurringCheckoutGateway>().SingleOrDefault();
        if (gateway is null) return "GatewayUnavailable";

        var preapproval = item.Topic == MercadoPagoNotification.Subscription;
        var payment = preapproval ? null : await gateway.ReadNotificationPaymentAsync(item.ResourceId,
            item.Topic == MercadoPagoNotification.AuthorizedPayment, cancellationToken);
        var externalId = preapproval ? item.ResourceId : payment?.SubscriptionId;
        if (externalId is null)
        {
            const string upgradePrefix = "servia-upgrade:";
            if (payment?.ExternalReference is { } referenceText && referenceText.StartsWith(upgradePrefix, StringComparison.Ordinal)
                && Guid.TryParseExact(referenceText[upgradePrefix.Length..], "N", out var changeId))
            {
                var change = await inbox.FindUpgradeAsync(changeId, cancellationToken);
                if (change is null) return "UnlinkedResource";
                if (change.Status != PlanChangeStatus.Requested) return "Superseded";
                tenantContext.SetTenant(change.TenantId);
                var planChanges = services.GetRequiredService<SubscriptionPlanChangeService>();
                return await planChanges.SynchronizeAsync(change.TenantId, change.SubscriptionId, cancellationToken,
                    propagateGatewayError: true) ? "UnconfirmedPayment" : "Current";
            }
            return "UnlinkedResource";
        }

        var authorization = await gateway.FindAsync(externalId, cancellationToken);
        if (authorization.Id != externalId) return "RequiresReconciliation";
        var subscription = await inbox.FindSubscriptionAsync(externalId, cancellationToken);
        if (subscription is null && Guid.TryParseExact(authorization.ExternalReference, "N", out var reference))
        {
            subscription = await inbox.FindDispatchedCheckoutAsync(reference, cancellationToken);
            if (subscription is not null && (subscription.CheckoutPayerEmail is not null
                || subscription.GatewayTrialPlanId is not null))
            {
                var matches = await gateway.SearchByCorrelationAsync(subscription.CheckoutPayerEmail,
                    subscription.GatewayTrialPlanId, reference.ToString("N"), cancellationToken);
                if (matches.Count != 1 || matches[0] != externalId) return "UnlinkedResource";
            }
        }
        if (subscription?.CheckoutKey is null) return "UnlinkedResource";
        tenantContext.SetTenant(subscription.TenantId);
        var synchronizer = services.GetRequiredService<SubscriptionSynchronizationService>();
        var state = (await synchronizer.ApplyAsync(subscription, authorization, cancellationToken, payment?.DueAt, preapproval)).State;

        return payment?.PaymentStatus switch
        {
            "approved" when state is "AwaitingFinancialConfirmation" or "PaymentPending" or "PaymentRejected"
                or "AwaitingExternalConfirmation" or "FirstPaymentRejected" => "UnconfirmedPayment",
            "rejected" when state is "AwaitingFinancialConfirmation" or "PaymentPending"
                or "AwaitingExternalConfirmation" => "UnconfirmedPayment",
            _ => state
        };
    }
}
