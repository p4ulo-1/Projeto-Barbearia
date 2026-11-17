using Microsoft.EntityFrameworkCore;
using Servia.Domain.Ai;
using Servia.Infrastructure.Persistence;

namespace Servia.Infrastructure.Repositories;

/// <summary>
/// Implementação EF Core de <see cref="IAiTenantIdentifierRepository"/>.
///
/// O filtro global restringe as consultas ao tenant corrente.
/// </summary>
public sealed class AiTenantIdentifierRepository(AppDbContext context) : IAiTenantIdentifierRepository
{
    public async Task<AiTenantIdentifier?> FindAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default) =>
        await context.AiTenantIdentifiers
            .Where(identifier => identifier.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(
        AiTenantIdentifier identifier,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identifier);

        await context.AiTenantIdentifiers.AddAsync(identifier, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
