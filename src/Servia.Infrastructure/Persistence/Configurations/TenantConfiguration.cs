using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servia.Domain.Tenancy;

namespace Servia.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeamento de <see cref="Tenant"/>.
///
/// O índice único em <c>Slug</c> é o definido no ARCH.md §6.2: é por ele que
/// o tenant é resolvido a partir do subdomínio.
///
/// <see cref="Tenant"/> não implementa <see cref="ITenantScoped"/>: é a raiz
/// do agregado, e um filtro por <c>TenantId</c> seria autorreferência.
/// </summary>
internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Tenants");

        builder.HasKey(tenant => tenant.Id);

        builder.Property(tenant => tenant.Id).ValueGeneratedNever();

        builder.Property(tenant => tenant.Slug).HasMaxLength(40).IsRequired();

        builder.Property(tenant => tenant.Name).HasMaxLength(150).IsRequired();

        builder.Property(tenant => tenant.Segment)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(tenant => tenant.Document).HasMaxLength(20).IsRequired();

        builder.Property(tenant => tenant.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(tenant => tenant.CreatedAt).IsRequired();

        // Expediente do estabelecimento: uma linha por dia da semana. É
        // independente dos profissionais; serve para exibir se o
        // estabelecimento atende ao público, não para agendar.
        builder.OwnsOne(tenant => tenant.Schedule, schedule =>
        {
            schedule.OwnsMany(s => s.Days, days =>
            {
                days.ToTable("TenantScheduleDays");
                days.WithOwner().HasForeignKey("TenantId");
                days.Property(day => day.DayOfWeek).HasConversion<string>().HasMaxLength(15).IsRequired();
                days.Property(day => day.IsOpen).IsRequired();
                days.Property(day => day.OpenTime);
                days.Property(day => day.CloseTime);
                days.HasKey("TenantId", nameof(EstablishmentDaySchedule.DayOfWeek));
                days.Ignore(day => day.Hours);
            });
        });

        // Índice crítico: resolução do tenant por subdomínio.
        builder.HasIndex(tenant => tenant.Slug).IsUnique();

        builder.Ignore(tenant => tenant.CanWrite);
        builder.Ignore(tenant => tenant.CanRead);
        builder.Ignore(tenant => tenant.IsOnTrial);
    }
}
