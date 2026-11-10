using Microsoft.EntityFrameworkCore;
using Servia.Domain.Identity;
using Servia.Infrastructure.Persistence;

namespace Servia.Infrastructure.Repositories;

/// <summary>
/// Implementação EF Core de <see cref="IUserRepository"/>.
///
/// A busca é escopada por tenant: o filtro global do contexto já restringe
/// as consultas, e o parâmetro explícito documenta a intenção (ADR-016).
/// </summary>
public sealed class UserRepository(AppDbContext context) : IUserRepository
{
    public async Task<User?> FindByEmailAsync(
        Guid tenantId,
        string email,
        CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();

        return await context.Users
            .FirstOrDefaultAsync(
                user => user.TenantId == tenantId && user.Email == normalized,
                cancellationToken);
    }

    public async Task<User?> FindByIdAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await context.Users
            .FirstOrDefaultAsync(
                user => user.TenantId == tenantId && user.Id == userId,
                cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        await context.Users.AddAsync(user, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
