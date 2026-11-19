using Microsoft.EntityFrameworkCore;
using Servia.Domain.Ai;
using Servia.Domain.Billing;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;

namespace Servia.Infrastructure.Repositories;

/// <summary>
/// Implementação EF Core de <see cref="IAiUsageRepository"/>.
///
/// O filtro global restringe as consultas ao tenant corrente.
/// </summary>
public sealed class AiUsageRepository(AppDbContext context, ISubscriptionRepository subscriptions) : IAiUsageRepository
{
    public async Task<UsageQuota?> FindAsync(
        Guid tenantId,
        QuotaResource resource,
        CancellationToken cancellationToken = default) =>
        await context.UsageQuotas
            .Where(quota => quota.TenantId == tenantId && quota.Resource == resource)
            .OrderByDescending(quota => quota.PeriodStart)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<Subscription?> FindCurrentSubscriptionAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default) =>
        subscriptions.FindCurrentAsync(tenantId, cancellationToken);

    public async Task AddAsync(UsageQuota quota, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(quota);

        await context.UsageQuotas.AddAsync(quota, cancellationToken);
    }

    public async Task<IReadOnlyList<AiCreditLot>> FindConsumableLotsAsync(
        Guid tenantId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        await context.AiCreditLots
            .Where(lot => lot.TenantId == tenantId
                          && lot.Consumed < lot.Quantity
                          && lot.ExpiresAt > now)
            .OrderBy(lot => lot.ExpiresAt)
            .ToListAsync(cancellationToken);

    public async Task AddLotAsync(AiCreditLot lot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lot);

        await context.AiCreditLots.AddAsync(lot, cancellationToken);
    }

    public async Task AddOperationLogAsync(
        AiOperationLog log,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(log);

        await context.AiOperationLogs.AddAsync(log, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
