using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servia.Domain.Appointments;

namespace Servia.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeamento de <see cref="Appointment"/>.
///
/// O índice <c>(TenantId, ProfessionalId, Date, Time)</c> é o definido no
/// ARCH.md §6.2: atende a consulta de agenda e a detecção de conflito.
/// </summary>
internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Appointments");

        builder.HasKey(appointment => appointment.Id);

        builder.Property(appointment => appointment.Id).ValueGeneratedNever();

        builder.Property(appointment => appointment.TenantId).IsRequired();

        builder.Property(appointment => appointment.CustomerId).IsRequired();

        builder.Property(appointment => appointment.ServiceId).IsRequired();

        builder.Property(appointment => appointment.ProfessionalId).IsRequired();

        builder.Property(appointment => appointment.Date).IsRequired();

        builder.Property(appointment => appointment.Time).IsRequired();

        builder.Property(appointment => appointment.DurationMinutes).IsRequired();

        builder.Property(appointment => appointment.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(appointment => appointment.CancellationReason).HasMaxLength(500);

        builder.Property(appointment => appointment.CreatedAt).IsRequired();

        // Índice crítico: consulta de agenda e detecção de conflito.
        builder.HasIndex(appointment => new
        {
            appointment.TenantId,
            appointment.ProfessionalId,
            appointment.Date,
            appointment.Time
        });

        // Isolamento: toda consulta por cliente é escopada por tenant.
        builder.HasIndex(appointment => new { appointment.TenantId, appointment.CustomerId });

        // Navegações não são expostas pelo agregado; o vínculo é por identificador.
        builder.Ignore(appointment => appointment.OccupiedRange);
        builder.Ignore(appointment => appointment.StartsAt);
        builder.Ignore(appointment => appointment.IsConfirmed);
    }
}
