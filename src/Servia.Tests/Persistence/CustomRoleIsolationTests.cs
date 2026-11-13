using Microsoft.EntityFrameworkCore;
using Servia.Domain.Identity;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;
using Servia.Infrastructure.Repositories;
using Servia.Infrastructure.Tenancy;

namespace Servia.Tests.Persistence;

/// <summary>
/// Seção 3: isolamento dos papéis personalizados por tenant.
///
/// Contra PostgreSQL 17, para provar que a coluna de permissões é
/// persistida e que um tenant nunca enxerga o papel de outro.
/// </summary>
public class CustomRoleIsolationTests : IAsyncLifetime
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    private readonly PostgresTestDatabase _database = new();

    public async Task InitializeAsync() => await _database.InitializeAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private static ITenantContext ContextFor(Guid tenantId)
    {
        var context = new TenantContext();
        context.SetTenant(tenantId);
        return context;
    }

    private async Task<CustomRole> CreateRoleAsync(Guid tenantId, string name)
    {
        await using var context = _database.CreateContext(ContextFor(tenantId));

        var role = CustomRole.Create(
            tenantId, name, [Permission.ViewServices, Permission.ManageAppointments]);

        context.CustomRoles.Add(role);
        await context.SaveChangesAsync();

        return role;
    }

    [Fact]
    public async Task Permissoes_Sao_Persistidas()
    {
        var role = await CreateRoleAsync(TenantA, "Atendente");

        await using var context = _database.CreateContext(ContextFor(TenantA));
        var reloaded = await context.CustomRoles.SingleAsync(item => item.Id == role.Id);

        Assert.True(reloaded.Has(Permission.ViewServices));
        Assert.True(reloaded.Has(Permission.ManageAppointments));
        Assert.Equal(2, reloaded.Permissions.Count);
    }

    [Fact]
    public async Task Tenant_Nao_Enxerga_Papel_De_Outro()
    {
        var roleA = await CreateRoleAsync(TenantA, "Atendente");

        await using var context = _database.CreateContext(ContextFor(TenantB));
        var repository = new CustomRoleRepository(context);

        var found = await repository.FindAsync(TenantB, roleA.Id);
        var list = await repository.ListAsync(TenantB);

        Assert.Null(found);
        Assert.Empty(list);
    }

    [Fact]
    public async Task Alteracao_De_Permissoes_E_Persistida()
    {
        var role = await CreateRoleAsync(TenantA, "Atendente");

        await using (var context = _database.CreateContext(ContextFor(TenantA)))
        {
            var tracked = await context.CustomRoles.SingleAsync(item => item.Id == role.Id);
            tracked.ReplacePermissions([Permission.ViewFinancialReports]);
            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext(ContextFor(TenantA)))
        {
            var reloaded = await context.CustomRoles.SingleAsync(item => item.Id == role.Id);

            Assert.True(reloaded.Has(Permission.ViewFinancialReports));
            Assert.False(reloaded.Has(Permission.ViewServices));
        }
    }

    [Fact]
    public async Task Revogar_Tokens_Do_Usuario_Ao_Mudar_Permissoes()
    {
        // Seção 1.1: um token emitido antes da mudança não pode continuar
        // válido; a revogação força a reemissão com as permissões atuais.
        var user = User.CreateForTenant(
            TenantA, "Bia", "bia@exemplo.com", "hash", Role.Professional, DateTimeOffset.UtcNow);

        await using (var context = _database.CreateContext(ContextFor(TenantA)))
        {
            context.Users.Add(user);
            context.RefreshTokens.Add(RefreshToken.Issue(
                TenantA, user.Id, "hash-1", DateTimeOffset.UtcNow, lifetimeDays: 7));
            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext(ContextFor(TenantA)))
        {
            var repository = new RefreshTokenRepository(context);

            await repository.RevokeAllForUserAsync(
                TenantA, user.Id, DateTimeOffset.UtcNow, "Permissões alteradas");
            await repository.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext(ContextFor(TenantA)))
        {
            var token = await context.RefreshTokens.SingleAsync(item => item.UserId == user.Id);

            Assert.True(token.IsRevoked);
            Assert.False(token.IsValid(DateTimeOffset.UtcNow));
        }
    }
}
