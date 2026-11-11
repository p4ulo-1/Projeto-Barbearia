using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servia.Domain.Operations;

namespace Servia.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeamento de <see cref="ProcessedEvent"/> (F17.4).
///
/// O índice único em `(TenantId, EventId, Source)` é a garantia de
/// idempotência: mesmo com duas requisições concorrentes do provedor, o
/// banco rejeita o segundo registro.
/// </summary>
internal sealed class ProcessedEventConfiguration : IEntityTypeConfiguration<ProcessedEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedEvent> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ProcessedEvents");
        builder.HasKey(processed => processed.Id);
        builder.Property(processed => processed.Id).ValueGeneratedNever();
        builder.Property(processed => processed.TenantId).IsRequired();
        builder.Property(processed => processed.EventId).HasMaxLength(200).IsRequired();
        builder.Property(processed => processed.Source).HasMaxLength(30).IsRequired();
        builder.Property(processed => processed.ProcessedAt).IsRequired();

        // Idempotencia: o mesmo evento nao entra duas vezes.
        builder.HasIndex(processed => new
        {
            processed.TenantId,
            processed.EventId,
            processed.Source
        }).IsUnique();

        // Retencao: consulta por data para limpeza periodica.
        builder.HasIndex(processed => processed.ProcessedAt);
    }
}
