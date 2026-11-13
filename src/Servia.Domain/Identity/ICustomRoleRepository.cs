namespace Servia.Domain.Identity;

/// <summary>
/// Contrato de persistência de papéis personalizados.
///
/// Todas as consultas são escopadas por tenant: um estabelecimento nunca
/// enxerga o papel personalizado de outro.
/// </summary>
public interface ICustomRoleRepository
{
    /// <summary>Lista os papéis personalizados do tenant.</summary>
    Task<IReadOnlyList<CustomRole>> ListAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Busca um papel personalizado do tenant, ou nulo.</summary>
    Task<CustomRole?> FindAsync(
        Guid tenantId,
        Guid customRoleId,
        CancellationToken cancellationToken = default);

    /// <summary>Registra um novo papel personalizado.</summary>
    Task AddAsync(CustomRole role, CancellationToken cancellationToken = default);

    /// <summary>Remove um papel personalizado.</summary>
    void Remove(CustomRole role);

    /// <summary>Persiste as alterações pendentes.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
