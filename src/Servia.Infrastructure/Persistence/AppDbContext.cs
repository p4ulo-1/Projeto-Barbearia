using Microsoft.EntityFrameworkCore;
using Servia.Domain.Appointments;
using Servia.Domain.Catalog;
using Servia.Domain.Customers;
using Servia.Domain.Team;

namespace Servia.Infrastructure.Persistence;

/// <summary>
/// Contexto de persistência do Servia.
///
/// O mapeamento fica em classes de configuração separadas
/// (<c>Persistence/Configurations</c>), uma por entidade, para manter o
/// contexto enxuto e cada mapeamento com uma única responsabilidade.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Appointment> Appointments => Set<Appointment>();

    public DbSet<Service> Services => Set<Service>();

    public DbSet<Professional> Professionals => Set<Professional>();

    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // Aplica todas as configurações do assembly de uma vez.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
