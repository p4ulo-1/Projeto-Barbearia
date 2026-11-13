using Microsoft.EntityFrameworkCore;
using Servia.Domain.Identity;
using Servia.Infrastructure.Persistence;

namespace Servia.Infrastructure.Repositories;

/// <summary>
/// Implementação EF Core de <see cref="IRefreshTokenRepository"/>.
///
/// A busca é pelo hash: o valor em claro nunca é persistido, então não há
/// como procurá-lo por ele.
/// </summary>
public sealed class RefreshTokenRepository(AppDbContext context) : IRefreshTokenRepository
{
    public async Task<RefreshToken?> FindByHashAsync(
        Guid tenantId,
        string tokenHash,
        CancellationToken cancellationToken = default) =>
        await context.RefreshTokens
            .FirstOrDefaultAsync(
                token => token.TenantId == tenantId && token.TokenHash == tokenHash,
                cancellationToken);

    /// <summary>
    /// Busca pelo hash sem exigir tenant (R2, Etapa 7).
    ///
    /// Justificativa: na renovacao de sessao o cliente ainda nao tem token
    /// de acesso, entao o tenant e desconhecido. O refresh token e opaco —
    /// so quem o possui conhece o hash. O resultado e usado apenas para
    /// descobrir o tenant e validar a sessao, nunca para expor dados.
    /// </summary>
    public async Task<RefreshToken?> FindByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default) =>
        await context.RefreshTokens
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                token => token.TokenHash == tokenHash,
                cancellationToken);

    public async Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);

        await context.RefreshTokens.AddAsync(token, cancellationToken);
    }

    /// <summary>
    /// Revoga todos os tokens de renovação ativos de um usuário.
    ///
    /// Usado quando as permissões efetivas mudam (papel personalizado
    /// alterado, atribuído, removido ou excluído): o próximo refresh passa a
    /// emitir um token com as permissões atuais, em vez de manter as antigas.
    /// </summary>
    public async Task RevokeAllForUserAsync(
        Guid tenantId,
        Guid userId,
        DateTimeOffset now,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var active = await context.RefreshTokens
            .Where(token => token.TenantId == tenantId
                && token.UserId == userId
                && token.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in active)
        {
            token.RevokeIfActive(now, reason);
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
