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

        // R2 (Etapa 7): unico bypass do filtro global no repositorio.
        //
        // Justificativa: o administrador da plataforma nao pertence a um
        // tenant (TenantId = Guid.Empty), entao o filtro o esconderia.
        // O bypass e restrito a tres condicoes simultaneas: tenant vazio,
        // papel PlatformAdmin e e-mail exato. Nenhum dado de tenant e
        // exposto por este caminho.
        if (tenantId == Guid.Empty)
        {
            return await context.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    user => user.TenantId == Guid.Empty
                            && user.Role == Role.PlatformAdmin
                            && user.Email == normalized,
                    cancellationToken);
        }

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
