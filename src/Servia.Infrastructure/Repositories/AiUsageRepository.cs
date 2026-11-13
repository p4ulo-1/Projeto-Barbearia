using Microsoft.EntityFrameworkCore;
using Servia.Domain.Billing;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;

namespace Servia.Infrastructure.Repositories;

/// <summary>
/// Implementação EF Core de <see cref="IAiUsageRepository"/>.
///
/// O filtro global restringe as consultas ao tenant corrente.
/// </summary>
public sealed class AiUsageRepository(AppDbContext context) : IAiUsageRepository
{
    public async Task<UsageQuota?> FindAsync(
        Guid tenantId,
        QuotaResource resource,
        CancellationToken cancellationToken = default) =>
        await context.UsageQuotas
            .Where(quota => quota.TenantId == tenantId && quota.Resource == resource)
            .OrderByDescending(quota => quota.PeriodStart)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<Subscription?> FindCurrentSubscriptionAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default) =>
        await context.Subscriptions
            .Where(subscription => subscription.TenantId == tenantId)
            .OrderByDescending(subscription => subscription.CurrentPeriodStart)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(UsageQuota quota, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(quota);

        await context.UsageQuotas.AddAsync(quota, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
