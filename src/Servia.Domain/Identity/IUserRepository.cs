namespace Servia.Domain.Identity;

/// <summary>
/// Contrato de persistência de usuários.
///
/// A busca por e-mail é <b>escopada por tenant</b>: o mesmo e-mail pode
/// existir em estabelecimentos diferentes, com registros independentes
/// (ADR-015). Não existe busca global por e-mail.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Busca o usuário pelo e-mail dentro do tenant.
    /// Retorna nulo quando não existe.
    /// </summary>
    Task<User?> FindByEmailAsync(
        Guid tenantId,
        string email,
        CancellationToken cancellationToken = default);

    /// <summary>Busca o usuário pelo identificador, dentro do tenant.</summary>
    Task<User?> FindByIdAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>Registra um novo usuário.</summary>
    Task AddAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>Persiste as alterações pendentes.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
