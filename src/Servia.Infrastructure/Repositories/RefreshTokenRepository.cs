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

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
