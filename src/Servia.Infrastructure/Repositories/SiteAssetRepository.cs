using Microsoft.EntityFrameworkCore;
using Servia.Domain.Sites;
using Servia.Infrastructure.Persistence;

namespace Servia.Infrastructure.Repositories;

/// <summary>
/// Implementação EF Core de <see cref="ISiteAssetRepository"/> (C11).
///
/// O filtro global restringe ao tenant corrente; o parâmetro explícito
/// documenta a intenção e protege caso o filtro seja desativado.
/// </summary>
public sealed class SiteAssetRepository(AppDbContext context) : ISiteAssetRepository
{
    public async Task<SiteAsset?> FindAsync(
        Guid tenantId,
        Guid assetId,
        CancellationToken cancellationToken = default) =>
        await context.SiteAssets
            .Where(asset => asset.TenantId == tenantId && asset.Id == assetId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<SiteAsset>> ListBySessionAsync(
        Guid tenantId,
        Guid onboardingSessionId,
        CancellationToken cancellationToken = default) =>
        await context.SiteAssets
            .Where(asset => asset.TenantId == tenantId
                && asset.OnboardingSessionId == onboardingSessionId)
            .OrderByDescending(asset => asset.UploadedAt)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(SiteAsset asset, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asset);

        await context.SiteAssets.AddAsync(asset, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
