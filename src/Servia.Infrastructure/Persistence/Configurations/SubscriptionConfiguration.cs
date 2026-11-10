using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servia.Domain.Tenancy;

namespace Servia.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeamento de <see cref="Subscription"/>.
///
/// A assinatura pertence ao billing da plataforma. O índice em
/// <c>(TenantId, Status)</c> atende à consulta da assinatura vigente de um
/// estabelecimento.
/// </summary>
internal sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Subscriptions");

        builder.HasKey(subscription => subscription.Id);

        builder.Property(subscription => subscription.Id).ValueGeneratedNever();

        builder.Property(subscription => subscription.TenantId).IsRequired();

        builder.Property(subscription => subscription.PlanId).IsRequired();

        builder.Property(subscription => subscription.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(subscription => subscription.CurrentPeriodStart).IsRequired();

        builder.Property(subscription => subscription.CurrentPeriodEnd).IsRequired();

        builder.Property(subscription => subscription.TrialEndsAt);

        builder.Property(subscription => subscription.GatewayCustomerId).HasMaxLength(100);

        builder.Property(subscription => subscription.GatewaySubscriptionId).HasMaxLength(100);

        builder.HasIndex(subscription => new { subscription.TenantId, subscription.Status });

        builder.Ignore(subscription => subscription.IsOnTrial);
        builder.Ignore(subscription => subscription.IsActive);
    }
}
