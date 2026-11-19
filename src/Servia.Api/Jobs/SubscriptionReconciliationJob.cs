using Microsoft.Extensions.Options;
using Servia.Application.Billing;
using Servia.Application.Jobs;
using Servia.Domain.Billing;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Repositories;

namespace Servia.Api.Jobs;

public sealed class SubscriptionReconciliationJob(IServiceScopeFactory scopes, TimeProvider clock,
    IOptions<SubscriptionReconciliationOptions> options, ILogger<SubscriptionReconciliationJob> logger) : IScheduledJob
{
    public string Name => "subscription-reconciliation";
    public TimeSpan Interval => TimeSpan.FromSeconds(Math.Clamp(options.Value.IntervalSeconds, 30, 3600));

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var batch = Math.Clamp(options.Value.BatchSize, 1, 20);
        for (var i = 0; i < batch && await ProcessNextAsync(cancellationToken); i++) { }
    }

    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var gateway = services.GetServices<IRecurringCheckoutGateway>().SingleOrDefault();
        if (gateway is null) return false;
        var repository = services.GetRequiredService<SubscriptionReconciliationRepository>();
        var claim = await repository.ClaimAsync(clock.GetUtcNow(), cancellationToken);
        if (claim is null) return false;

        var subscription = claim.Subscription;
        var tenant = services.GetRequiredService<ITenantContext>();
        string result;
        TimeSpan? cooldown = null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(90));
        try
        {
            tenant.SetTenant(subscription.TenantId);
            var synchronizer = services.GetRequiredService<SubscriptionSynchronizationService>();
            var verified = await VerifyAsync(subscription, gateway, synchronizer, timeout.Token);
            result = verified.State;
            subscription = verified.Subscription;
        }
        catch (RecurringCheckoutGatewayException exception) when (exception.RateLimited)
        {
            result = "RateLimited";
            cooldown = exception.RetryAfter > TimeSpan.Zero ? exception.RetryAfter : TimeSpan.FromMinutes(5);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            result = "VerificationFailed";
            logger.LogWarning("Falha na reconciliação da assinatura {SubscriptionId}.", subscription.Id);
        }
        finally
        {
            tenant.Clear();
        }
        var now = clock.GetUtcNow();
        var healthy = result is "Trialing" or "Active" or "Renewed" or "Current" or "Failed"
            or "Superseded" or "Cancelled" or "CancellationScheduled";
        var limit = Math.Clamp(options.Value.MaxBackoffAttempts, 1, 20);
        var attempts = healthy ? 0 : Math.Min(limit, claim.Attempts + 1);
        var intervention = attempts >= limit || result is "MissingCorrelationData" or "AmbiguousMatch";
        var regular = now.AddMinutes(Math.Clamp(options.Value.HealthyCheckMinutes, 5, 1440));
        var due = subscription.IsOnTrial ? subscription.TrialDeadline : subscription.ChargeDeadline;
        var next = healthy
            ? due <= now ? now.Add(Interval) : due < regular ? due!.Value : regular
            : intervention ? now.AddDays(1) : now.AddMinutes(Math.Min(360, 5 * Math.Pow(2, attempts - 1)));
        if (result == "PlanChangePending" && !intervention && due > now && due < next) next = due.Value;
        DateTimeOffset? rateLimitedUntil = cooldown.HasValue ? now.Add(cooldown.Value) : null;
        if (rateLimitedUntil > next) next = rateLimitedUntil.Value;
        await repository.FinishAsync(claim, result, now, next, attempts, intervention, rateLimitedUntil, cancellationToken);
        return true;
    }

    private static async Task<LifecycleResult> VerifyAsync(Subscription subscription, IRecurringCheckoutGateway gateway,
        SubscriptionSynchronizationService synchronizer, CancellationToken cancellationToken)
    {
        var externalId = subscription.GatewaySubscriptionId;
        if (externalId is null)
        {
            if (subscription.CheckoutPayerEmail is null && subscription.GatewayTrialPlanId is null)
            {
                return new(subscription, "MissingCorrelationData");
            }
            var matches = await gateway.SearchByCorrelationAsync(subscription.CheckoutPayerEmail, subscription.GatewayTrialPlanId,
                subscription.Id.ToString("N"), cancellationToken);
            if (matches.Count != 1)
            {
                return new(subscription, matches.Count == 0 ? "NoRemoteMatch" : "AmbiguousMatch");
            }
            externalId = matches[0];
        }
        var authorization = await gateway.FindAsync(externalId, cancellationToken);
        return authorization.Id == externalId
            ? await synchronizer.ApplyAsync(subscription, authorization, cancellationToken)
            : new(subscription, "RequiresReconciliation");
    }
}

public sealed class SubscriptionReconciliationOptions
{
    public int IntervalSeconds { get; set; } = 60;
    public int BatchSize { get; set; } = 10;
    public int HealthyCheckMinutes { get; set; } = 360;
    public int MaxBackoffAttempts { get; set; } = 10;
}
