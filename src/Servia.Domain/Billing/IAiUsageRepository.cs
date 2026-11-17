using Servia.Domain.Ai;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Billing;

/// <summary>
/// Persistência das cotas de consumo e dos lotes de créditos de IA
/// (gestão de IA, §5 e §7).
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

    /// <summary>
    /// Lotes de créditos adicionais do tenant que ainda têm saldo, ordenados
    /// pela expiração — o que expira antes é consumido primeiro (§5.4).
    /// </summary>
    Task<IReadOnlyList<AiCreditLot>> FindConsumableLotsAsync(
        Guid tenantId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    /// <summary>Registra um novo lote de créditos.</summary>
    Task AddLotAsync(AiCreditLot lot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra a operação de IA para auditoria e reconciliação (§5.3).
    /// </summary>
    Task AddOperationLogAsync(
        AiOperationLog log,
        CancellationToken cancellationToken = default);

    /// <summary>Persiste as alterações pendentes.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
