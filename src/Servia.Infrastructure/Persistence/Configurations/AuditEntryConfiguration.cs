using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servia.Domain.Auditing;

namespace Servia.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeamento de <see cref="AuditEntry"/>.
///
/// O índice em <c>(TenantId, OccurredAt)</c> atende à consulta de auditoria
/// por período (ARCH.md §6.2).
///
/// <b>Imutabilidade (ADR-013):</b> a entidade não tem métodos de alteração.
/// O mapeamento reforça isso: nenhuma propriedade tem setter público, e o
/// contexto não expõe operação de atualização para esta tabela.
/// </summary>
internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AuditEntries");

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id).ValueGeneratedNever();

        builder.Property(entry => entry.TenantId);

        builder.Property(entry => entry.ActorId);

        builder.Property(entry => entry.Action).HasMaxLength(100).IsRequired();

        builder.Property(entry => entry.EntityType).HasMaxLength(100).IsRequired();

        builder.Property(entry => entry.EntityId).IsRequired();

        builder.Property(entry => entry.Changes);

        builder.Property(entry => entry.OccurredAt).IsRequired();

        builder.Property(entry => entry.IpAddress).HasMaxLength(45);

        // Consulta de auditoria por período.
        builder.HasIndex(entry => new { entry.TenantId, entry.OccurredAt });

        builder.HasIndex(entry => new { entry.EntityType, entry.EntityId });

        builder.Ignore(entry => entry.IsPlatformAction);
    }
}
