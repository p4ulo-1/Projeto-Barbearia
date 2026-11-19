using Microsoft.EntityFrameworkCore;
using Servia.Domain.Billing;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;

namespace Servia.Infrastructure.Repositories;

public sealed class SubscriptionReconciliationRepository(AppDbContext context)
{
    public async Task<SubscriptionReconciliationClaim?> ClaimAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var candidates = await context.Subscriptions.FromSqlInterpolated($"""
            SELECT s.*
            FROM "Subscriptions" AS s
            LEFT JOIN "SubscriptionReconciliation" AS r ON r."SubscriptionId" = s."Id"
            WHERE s."CheckoutDispatchedAt" IS NOT NULL AND s."CheckoutKey" IS NOT NULL
              AND s."CancellationEffectiveOn" IS NULL
              AND s."Status" IN ('PendingCheckout', 'PendingConfirmation', 'AuthorizedAwaitingFirstPayment',
                                 'Trialing', 'Active', 'PastDue')
              AND COALESCE(r."NextCheckAt", s."CheckoutDispatchedAt" + INTERVAL '1 minute') <= {now}
              AND NOT EXISTS (
                  SELECT 1 FROM "SubscriptionReconciliation" AS cooldown
                  WHERE cooldown."RateLimitedUntil" > {now}
              )
            ORDER BY COALESCE(r."NextCheckAt", s."CheckoutDispatchedAt" + INTERVAL '1 minute'), s."Id"
            LIMIT 1 FOR UPDATE OF s SKIP LOCKED
            """).IgnoreQueryFilters().AsNoTracking().ToListAsync(cancellationToken);
        var subscription = candidates.SingleOrDefault();
        if (subscription is null) return null;

        var rows = context.Set<SubscriptionReconciliation>();
        var state = await rows.FindAsync([subscription.Id], cancellationToken);
        if (state is null)
        {
            state = new SubscriptionReconciliation { SubscriptionId = subscription.Id };
            rows.Add(state);
        }
        if (state.NextCheckAt > now) return null;
        state.NextCheckAt = now.AddMinutes(5);
        state.LeaseId = Guid.NewGuid();
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(subscription, state.LeaseId.Value, state.Attempts);
    }

    public Task FinishAsync(SubscriptionReconciliationClaim claim, string result, DateTimeOffset checkedAt,
        DateTimeOffset nextCheck, int attempts, bool intervention, DateTimeOffset? rateLimitedUntil,
        CancellationToken cancellationToken) =>
        context.Set<SubscriptionReconciliation>()
            .Where(row => row.SubscriptionId == claim.Subscription.Id && row.LeaseId == claim.LeaseId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.LeaseId, (Guid?)null)
                .SetProperty(row => row.Result, result)
                .SetProperty(row => row.Attempts, attempts)
                .SetProperty(row => row.NeedsIntervention, intervention)
                .SetProperty(row => row.LastCheckedAt, checkedAt)
                .SetProperty(row => row.RateLimitedUntil, rateLimitedUntil)
                .SetProperty(row => row.NextCheckAt, nextCheck), cancellationToken);
}

public sealed record SubscriptionReconciliationClaim(Subscription Subscription, Guid LeaseId, int Attempts);
