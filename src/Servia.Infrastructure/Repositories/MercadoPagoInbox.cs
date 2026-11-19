using Microsoft.EntityFrameworkCore;
using Servia.Domain.Operations;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;

namespace Servia.Infrastructure.Repositories;

public sealed class MercadoPagoInbox(AppDbContext context)
{
    public async Task EnqueueAsync(MercadoPagoNotification item, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "MercadoPagoInbox"
                ("Id", "ExternalId", "Topic", "ResourceId", "RequestId", "SignatureTimestamp",
                 "ReceivedAt", "AvailableAt", "CompletedAt", "Attempts", "Result")
            VALUES ({item.Id}, {item.ExternalId}, {item.Topic}, {item.ResourceId}, {item.RequestId},
                    {item.SignatureTimestamp}, {item.ReceivedAt}, {item.AvailableAt},
                    {item.CompletedAt}, 0, {item.Result})
            ON CONFLICT DO NOTHING
            """, cancellationToken);
    }

    public async Task<MercadoPagoNotification?> ClaimAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var candidates = await context.Set<MercadoPagoNotification>().FromSqlInterpolated($"""
            SELECT * FROM "MercadoPagoInbox"
            WHERE "CompletedAt" IS NULL AND "AvailableAt" <= {now}
            ORDER BY "AvailableAt", "ReceivedAt", "Id"
            LIMIT 1 FOR UPDATE SKIP LOCKED
            """).ToListAsync(cancellationToken);
        var item = candidates.SingleOrDefault();
        if (item is not null)
        {
            item.LeaseId = Guid.NewGuid();
            item.AvailableAt = now.AddMinutes(5);
            item.Attempts++;
            await context.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        if (item is not null) context.Entry(item).State = EntityState.Detached;
        return item;
    }

    public Task FinishAsync(MercadoPagoNotification item, string result, bool retry,
        DateTimeOffset now, CancellationToken cancellationToken, TimeSpan? retryAfter = null)
    {
        var next = retry ? now.AddSeconds(Math.Min(3600, 30 * Math.Pow(2, Math.Min(7, item.Attempts - 1)))) : now;
        if (retryAfter is { } delay && now.Add(delay) > next) next = now.Add(delay);
        return context.Set<MercadoPagoNotification>()
            .Where(row => row.Id == item.Id && row.LeaseId == item.LeaseId && row.CompletedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.LeaseId, (Guid?)null)
                .SetProperty(row => row.Result, result)
                .SetProperty(row => row.AvailableAt, next)
                .SetProperty(row => row.CompletedAt, retry ? (DateTimeOffset?)null : now), cancellationToken);
    }

    public Task<Subscription?> FindSubscriptionAsync(string externalId, CancellationToken cancellationToken) =>
        context.Subscriptions.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(item => item.GatewaySubscriptionId == externalId, cancellationToken);

    public Task<Subscription?> FindDispatchedCheckoutAsync(Guid reference, CancellationToken cancellationToken) =>
        context.Subscriptions.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == reference && item.CheckoutKey != null && item.CheckoutDispatchedAt != null
            && item.GatewaySubscriptionId == null && item.Status == SubscriptionStatus.PendingCheckout, cancellationToken);
}
