using Servia.Domain.Tenancy;

namespace Servia.Domain.Billing;

/// <summary>
/// Persistência das cotas de consumo (política provisória de IA).
///
/// Todas as consultas são escopadas por tenant.
/// </summary>
public interface IAiUsageRepository
{
    /// <summary>Cota do recurso para o tenant, ou nula.</summary>
    Task<UsageQuota?> FindAsync(
        Guid tenantId,
        QuotaResource resource,
        CancellationToken cancellationToken = default);

    /// <summary>Assinatura vigente do tenant, ou nula.</summary>
    Task<Subscription?> FindCurrentSubscriptionAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Registra uma nova cota.</summary>
    Task AddAsync(UsageQuota quota, CancellationToken cancellationToken = default);

    /// <summary>Persiste as alterações pendentes.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
