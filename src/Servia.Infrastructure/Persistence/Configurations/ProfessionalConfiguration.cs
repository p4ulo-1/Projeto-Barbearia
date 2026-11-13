using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servia.Domain.Team;

namespace Servia.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeamento de <see cref="Professional"/>.
///
/// A jornada é <b>composição</b>: mapeada como <i>owned type</i>, sem tabela
/// própria. As colunas da jornada vivem na tabela <c>Professionals</c>, o que
/// reflete a decisão de modelagem de que a jornada não existe sem o
/// profissional (ver docs/av2/modelo-de-classes.md §3).
/// </summary>
internal sealed class ProfessionalConfiguration : IEntityTypeConfiguration<Professional>
{
    public void Configure(EntityTypeBuilder<Professional> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Professionals");

        builder.HasKey(professional => professional.Id);

        // Chave alternativa (TenantId, Id): alvo das FKs compostas do agendamento.
        builder.HasAlternateKey(professional => new { professional.TenantId, professional.Id });

        builder.Property(professional => professional.Id).ValueGeneratedNever();

        builder.Property(professional => professional.TenantId).IsRequired();

        builder.Property(professional => professional.UserId);

        builder.Property(professional => professional.Name).HasMaxLength(150).IsRequired();

        builder.Property(professional => professional.Title).HasMaxLength(100);

        builder.Property(professional => professional.Bio).HasMaxLength(1000);

        builder.Property(professional => professional.PhotoUrl).HasMaxLength(500);

        builder.Property(professional => professional.IsActive).IsRequired();

        // Atuação profissional: define se a pessoa ocupa vaga no plano,
        // independentemente do perfil de acesso. Default true preserva os
        // registros existentes (todos eram prestadores).
        builder.Property(professional => professional.IsServiceProvider)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Ignore(professional => professional.OccupiesPlanSlot);

        // Composição: a jornada não tem tabela própria.
        builder.OwnsOne(professional => professional.Schedule, schedule =>
        {
            schedule.Property(work => work.StartTime)
                .HasColumnName("Schedule_StartTime")
                .IsRequired();

            schedule.Property(work => work.EndTime)
                .HasColumnName("Schedule_EndTime")
                .IsRequired();

            schedule.Property(work => work.SlotIntervalMinutes)
                .HasColumnName("Schedule_SlotIntervalMinutes")
                .IsRequired();

            // WorkDays é um conjunto de DayOfWeek: persistido como CSV.
            schedule.Property(work => work.WorkDays)
                .HasColumnName("Schedule_WorkDays")
                .HasConversion(
                    days => string.Join(',', days.Select(day => (int)day)),
                    text => ParseWorkDays(text),
                    new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<IReadOnlySet<DayOfWeek>>(
                        (left, right) => left.SetEquals(right),
                        days => days.Aggregate(0, (hash, day) => hash ^ day.GetHashCode())))
                .HasMaxLength(50)
                .IsRequired();

            schedule.Ignore(work => work.WorkingHours);
        });

        builder.HasIndex(professional => new { professional.TenantId, professional.IsActive });
    }

    private static IReadOnlySet<DayOfWeek> ParseWorkDays(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new HashSet<DayOfWeek>();
        }

        return text
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => (DayOfWeek)int.Parse(value, System.Globalization.CultureInfo.InvariantCulture))
            .ToHashSet();
    }
}
