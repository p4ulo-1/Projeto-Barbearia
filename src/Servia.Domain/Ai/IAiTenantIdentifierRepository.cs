namespace Servia.Domain.Ai;

/// <summary>
/// Persistência do identificador opaco do tenant perante o provedor de IA
/// (gestão de IA, §4.2; plano de implementação, Etapa 2).
///
/// Todas as consultas são escopadas por tenant.
/// </summary>
public interface IAiTenantIdentifierRepository
{
    /// <summary>Identificador persistido do tenant, ou nulo.</summary>
    Task<AiTenantIdentifier?> FindAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Registra um novo identificador.</summary>
    Task AddAsync(
        AiTenantIdentifier identifier,
        CancellationToken cancellationToken = default);

    /// <summary>Persiste as alterações pendentes.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
