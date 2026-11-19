using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servia.Domain.Tenancy;

namespace Servia.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeamento de <see cref="PlanChange"/> (F16.5).
///
/// O histórico é imutável: a entidade não expõe métodos de alteração, e o
/// índice por tenant e data permite reconstruir a linha do tempo.
/// </summary>
internal sealed class PlanChangeConfiguration : IEntityTypeConfiguration<PlanChange>
{
    public void Configure(EntityTypeBuilder<PlanChange> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("PlanChanges");
        builder.HasKey(change => change.Id);
        builder.Property(change => change.Id).ValueGeneratedNever();
        builder.Property(change => change.TenantId).IsRequired();
        builder.Property(change => change.SubscriptionId).IsRequired();
        builder.Property(change => change.FromPlanId);
        builder.Property(change => change.ToPlanId).IsRequired();
        builder.Property(change => change.Reason).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(change => change.ChangedAt).IsRequired();
        builder.Property(change => change.Status).HasConversion<string>().HasMaxLength(20)
            .HasDefaultValue(PlanChangeStatus.Effective);
        builder.Property(change => change.RequestedPrice).HasPrecision(18, 2);
        builder.Property(change => change.RequestedCurrency).HasMaxLength(3);
        builder.Property(change => change.SourcePrice).HasPrecision(18, 2);
        builder.Property(change => change.AdditionalAmount).HasPrecision(18, 2);
        builder.Property(change => change.AdditionalPaymentId).HasMaxLength(100);
        builder.Property(change => change.Issue).HasMaxLength(64);
        builder.Property<uint>("Version").IsRowVersion();
        builder.HasIndex(change => new { change.TenantId, change.OperationKey }).IsUnique()
            .HasFilter("\"OperationKey\" IS NOT NULL");
        builder.HasIndex(change => change.AdditionalPaymentId).IsUnique().HasFilter("\"AdditionalPaymentId\" IS NOT NULL");
        builder.HasIndex(change => new { change.TenantId, change.SubscriptionId }, "UX_PlanChanges_RequestedSubscription")
            .IsUnique().HasFilter("\"Status\" = 'Requested'");

        // Consulta do histórico: por tenant, mais recente primeiro.
        builder.HasIndex(change => new { change.TenantId, change.ChangedAt });
    }
}
