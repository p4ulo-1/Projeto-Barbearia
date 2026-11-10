using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Servia.Domain.Appointments;
using Servia.Domain.Auditing;
using Servia.Domain.Catalog;
using Servia.Domain.Customers;
using Servia.Domain.Identity;
using Servia.Domain.Team;
using Servia.Domain.Tenancy;

namespace Servia.Infrastructure.Persistence;

/// <summary>
/// Contexto de persistência do Servia.
///
/// Aplica o <b>filtro global por tenant</b> (ADR-016): toda entidade que
/// implementa <see cref="ITenantScoped"/> tem suas consultas restritas ao
/// tenant corrente, sem que cada consulta precise lembrar de filtrar.
///
/// <b>Comportamento sem tenant:</b> quando não há tenant resolvido, o filtro
/// nega acesso a dados de negócio. Isso é intencional — falhar fechado é
/// melhor do que vazar dados. Operações administrativas da plataforma devem
/// usar um contexto explícito, nunca um tenant arbitrário.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext)
    : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Plan> Plans => Set<Plan>();

    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    public DbSet<Appointment> Appointments => Set<Appointment>();

    public DbSet<Service> Services => Set<Service>();

    public DbSet<Professional> Professionals => Set<Professional>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Invitation> Invitations => Set<Invitation>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        ApplyTenantFilter(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Aplica o filtro global a todas as entidades que implementam
    /// <see cref="ITenantScoped"/>, sem precisar listá-las uma a uma.
    /// </summary>
    private void ApplyTenantFilter(ModelBuilder modelBuilder)
    {
        var tenantScopedTypes = modelBuilder.Model
            .GetEntityTypes()
            .Where(entityType => typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType));

        foreach (var entityType in tenantScopedTypes)
        {
            var parameter = Expression.Parameter(entityType.ClrType, "entity");
            var tenantProperty = Expression.Property(parameter, nameof(ITenantScoped.TenantId));

            // entity.TenantId == tenantContext.TenantId
            // Quando TenantId é nulo, a comparação falha e nada é retornado.
            var contextProperty = Expression.Property(
                Expression.Constant(this),
                nameof(CurrentTenantId));

            var body = Expression.Equal(tenantProperty, contextProperty);

            var filter = Expression.Lambda(body, parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
        }
    }

    /// <summary>
    /// Tenant corrente, exposto para o filtro global.
    /// Sem tenant resolvido, retorna um valor que não corresponde a nenhum
    /// registro — o filtro nega acesso em vez de liberar.
    /// </summary>
    public Guid CurrentTenantId => tenantContext.TenantId ?? Guid.Empty;
}
