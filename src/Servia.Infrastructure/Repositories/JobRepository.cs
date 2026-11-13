using Microsoft.EntityFrameworkCore;
using Servia.Domain.Appointments;
using Servia.Domain.Billing;
using Servia.Domain.Memberships;
using Servia.Domain.Messaging;
using Servia.Domain.Operations;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;

namespace Servia.Infrastructure.Repositories;

/// <summary>
/// Implementação EF Core de <see cref="IJobRepository"/> (F19).
///
/// As rotinas processam todos os tenants de uma vez. Por isso as
/// consultas usam `IgnoreQueryFilters`: o filtro global depende de um
/// tenant resolvido, e a rotina não tem um. O isolamento é preservado
/// porque cada operação atua sobre o registro que já carrega seu próprio
/// `TenantId` — nenhuma consulta cruza dados entre tenants.
/// </summary>
public sealed class JobRepository(AppDbContext context) : IJobRepository
{
    public async Task<IReadOnlyList<Notification>> GetDueNotificationsAsync(
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken = default) =>
        await context.Notifications
            .IgnoreQueryFilters()
            .Where(notification => notification.Status == NotificationStatus.Scheduled
                                   && (notification.ScheduledAt == null
                                       || notification.ScheduledAt <= now))
            .OrderBy(notification => notification.ScheduledAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PlatformInvoice>> GetOverdueInvoicesAsync(
        DateOnly today,
        int limit,
        CancellationToken cancellationToken = default) =>
        await context.PlatformInvoices
            .IgnoreQueryFilters()
            .Where(invoice => invoice.Status == InvoiceStatus.Pending && invoice.DueDate < today)
            .OrderBy(invoice => invoice.DueDate)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Subscription>> GetPastDueSubscriptionsAsync(
        int limit,
        CancellationToken cancellationToken = default) =>
        await context.Subscriptions
            .IgnoreQueryFilters()
            .Where(subscription => subscription.Status == SubscriptionStatus.PastDue)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Subscription>> GetExpiredTrialsAsync(
        DateOnly today,
        int limit,
        CancellationToken cancellationToken = default) =>
        await context.Subscriptions
            .IgnoreQueryFilters()
            .Where(subscription => subscription.Status == SubscriptionStatus.Trialing
                                   && subscription.TrialEndsAt != null
                                   && subscription.TrialEndsAt < today)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ScheduleLock>> GetExpiredLocksAsync(
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken = default) =>
        await context.ScheduleLocks
            .IgnoreQueryFilters()
            .Where(scheduleLock => scheduleLock.IsActive && scheduleLock.ExpiresAt <= now)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Membership>> GetExpiredMembershipsAsync(
        DateOnly today,
        int limit,
        CancellationToken cancellationToken = default) =>
        await context.Memberships
            .IgnoreQueryFilters()
            .Where(membership => membership.Status == MembershipStatus.Active
                                 && membership.CurrentPeriodEnd < today)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProcessedEvent>> GetExpiredProcessedEventsAsync(
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var threshold = now.AddDays(-ProcessedEvent.RetentionDays);

        return await context.ProcessedEvents
            .IgnoreQueryFilters()
            .Where(processed => processed.ProcessedAt < threshold)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public void RemoveProcessedEvents(IEnumerable<ProcessedEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        context.ProcessedEvents.RemoveRange(events);
    }

    public Task<int> RemoveExpiredBookingAttemptsAsync(
        DateTimeOffset before,
        CancellationToken cancellationToken = default) =>
        context.PublicBookingAttempts
            .Where(attempt => attempt.WindowEnd < before)
            .ExecuteDeleteAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
