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

        builder.ToTable("Subscriptions", table => table.HasCheckConstraint(
            "CK_Subscriptions_CheckoutState",
            "\"CheckoutKey\" IS NULL OR (\"Status\" IN ('PendingCheckout', 'PendingConfirmation', 'AuthorizedAwaitingFirstPayment', 'Cancelled', 'Expired')"
            + " OR (\"Status\" = 'Trialing' AND \"TrialStartedAtUtc\" IS NOT NULL AND \"TrialEndsAtUtc\" IS NOT NULL AND \"GatewayTrialPlanId\" IS NOT NULL AND \"GatewaySubscriptionId\" IS NOT NULL)"
            + " OR (\"Status\" IN ('Active', 'PastDue') AND \"FirstPaymentId\" IS NOT NULL AND \"GatewaySubscriptionId\" IS NOT NULL))"));

        builder.HasKey(subscription => subscription.Id);

        builder.Property(subscription => subscription.Id).ValueGeneratedNever();

        builder.Property(subscription => subscription.TenantId).IsRequired();

        builder.Property(subscription => subscription.PlanId).IsRequired();

        builder.Property(subscription => subscription.Status)
            .HasConversion<string>()
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(subscription => subscription.CurrentPeriodStart).IsRequired();

        builder.Property(subscription => subscription.CurrentPeriodEnd).IsRequired();

        builder.Property(subscription => subscription.TrialEndsAt);

        builder.Property(subscription => subscription.GatewayCustomerId).HasMaxLength(100);

        builder.Property(subscription => subscription.GatewaySubscriptionId).HasMaxLength(100);
        builder.Property(subscription => subscription.FirstPaymentId).HasMaxLength(100);
        builder.Property(subscription => subscription.ContractPriceAmount).HasPrecision(18, 2);
        builder.Property(subscription => subscription.ContractCurrency).HasMaxLength(3);
        builder.Property<string>("CheckoutUrl").HasMaxLength(2048);
        builder.Property(subscription => subscription.CheckoutPayerEmail).HasMaxLength(320);
        builder.Property(subscription => subscription.GatewayTrialPlanId).HasMaxLength(100);
        builder.Property<uint>("Version").IsRowVersion();

        builder.HasIndex(subscription => new { subscription.TenantId, subscription.Status });
        builder.HasIndex(subscription => subscription.TenantId, "UX_Subscriptions_EffectiveTenant")
            .IsUnique().HasFilter("\"Status\" IN ('Trialing', 'Active') OR (\"Status\" = 'PastDue' AND \"TrialEndsAt\" IS NULL)");
        builder.HasIndex(subscription => subscription.TenantId, "UX_Subscriptions_PendingTenant")
            .IsUnique().HasFilter("\"Status\" IN ('PendingCheckout', 'PendingConfirmation', 'AuthorizedAwaitingFirstPayment')");
        builder.HasIndex(subscription => new { subscription.TenantId, subscription.CheckoutKey })
            .IsUnique().HasFilter("\"CheckoutKey\" IS NOT NULL");
        builder.HasIndex(subscription => subscription.GatewaySubscriptionId)
            .IsUnique().HasFilter("\"GatewaySubscriptionId\" IS NOT NULL");
        builder.HasIndex(subscription => subscription.FirstPaymentId)
            .IsUnique().HasFilter("\"FirstPaymentId\" IS NOT NULL");
        builder.HasIndex(subscription => new { subscription.CheckoutDispatchedAt, subscription.Id }, "IX_Subscriptions_Reconciliation")
            .HasFilter("\"CheckoutDispatchedAt\" IS NOT NULL AND \"CheckoutKey\" IS NOT NULL AND \"CancellationEffectiveOn\" IS NULL"
                + " AND \"Status\" IN ('PendingCheckout', 'PendingConfirmation', 'AuthorizedAwaitingFirstPayment', 'Trialing', 'Active', 'PastDue')");

        builder.Ignore(subscription => subscription.IsOnTrial);
        builder.Ignore(subscription => subscription.IsActive);
    }
}
