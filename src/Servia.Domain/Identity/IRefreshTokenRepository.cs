namespace Servia.Domain.Identity;

/// <summary>
/// Contrato de persistência de tokens de renovação.
///
/// A busca é pelo hash do token: o valor em claro nunca é persistido, então
/// não há como procurá-lo por ele.
/// </summary>
public interface IRefreshTokenRepository
{
    /// <summary>Busca o token pelo hash, dentro do tenant.</summary>
    Task<RefreshToken?> FindByHashAsync(
        Guid tenantId,
        string tokenHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca o token apenas pelo hash, sem exigir o tenant.
    ///
    /// Usado na renovacao de sessao: o cliente ainda nao tem token de
    /// acesso, entao o tenant e resolvido a partir do proprio refresh
    /// token — que e opaco e so o servidor conhece o hash.
    /// </summary>
    Task<RefreshToken?> FindByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);

    /// <summary>Registra um novo token.</summary>
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default);

    /// <summary>Persiste as alterações pendentes.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
