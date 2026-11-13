using Microsoft.EntityFrameworkCore;
using Servia.Domain.Identity;
using Servia.Infrastructure.Persistence;

namespace Servia.Infrastructure.Repositories;

/// <summary>
/// Implementação EF Core de <see cref="ICustomRoleRepository"/>.
///
/// O filtro global restringe as consultas ao tenant corrente; o parâmetro
/// explícito documenta a intenção e protege caso o filtro seja desativado.
/// </summary>
public sealed class CustomRoleRepository(AppDbContext context) : ICustomRoleRepository
{
    public async Task<IReadOnlyList<CustomRole>> ListAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default) =>
        await context.CustomRoles
            .Where(role => role.TenantId == tenantId)
            .OrderBy(role => role.Name)
            .ToListAsync(cancellationToken);

    public async Task<CustomRole?> FindAsync(
        Guid tenantId,
        Guid customRoleId,
        CancellationToken cancellationToken = default) =>
        await context.CustomRoles
            .FirstOrDefaultAsync(
                role => role.TenantId == tenantId && role.Id == customRoleId,
                cancellationToken);

    public async Task AddAsync(CustomRole role, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(role);

        await context.CustomRoles.AddAsync(role, cancellationToken);
    }

    public void Remove(CustomRole role)
    {
        ArgumentNullException.ThrowIfNull(role);

        context.CustomRoles.Remove(role);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
