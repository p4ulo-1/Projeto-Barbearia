using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servia.Domain.Billing;

namespace Servia.Infrastructure.Persistence.Configurations;

/// <summary>Mapeamento de <see cref="PlatformInvoice"/> e suas linhas.</summary>
internal sealed class PlatformInvoiceConfiguration : IEntityTypeConfiguration<PlatformInvoice>
{
    public void Configure(EntityTypeBuilder<PlatformInvoice> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("PlatformInvoices");
        builder.HasKey(invoice => invoice.Id);
        builder.Property(invoice => invoice.Id).ValueGeneratedNever();
        builder.Property(invoice => invoice.TenantId).IsRequired();
        builder.Property(invoice => invoice.SubscriptionId).IsRequired();
        builder.Property(invoice => invoice.PeriodStart).IsRequired();
        builder.Property(invoice => invoice.PeriodEnd).IsRequired();
        builder.Property(invoice => invoice.DueDate).IsRequired();
        builder.Property(invoice => invoice.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(invoice => invoice.PaidAt);
        builder.Property(invoice => invoice.GatewayPaymentId).HasMaxLength(100);

        // Linhas da fatura: entidade propria, com FK para a fatura.
        // Mapeadas como owned collection com chave sintetica.
        builder.OwnsMany(invoice => invoice.Lines, lines =>
        {
            lines.ToTable("PlatformInvoiceLines");
            lines.WithOwner().HasForeignKey("PlatformInvoiceId");
            lines.HasKey(line => line.Id);
            lines.Property(line => line.Id).ValueGeneratedNever();
            lines.Property(line => line.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
            lines.Property(line => line.Description).HasMaxLength(200).IsRequired();
            lines.Property(line => line.Amount)
                .HasConversion(
                    money => money.Amount,
                    value => Servia.Domain.Common.Money.From(value))
                .HasColumnName("Amount")
                .HasPrecision(18, 2)
                .IsRequired();
            lines.Ignore(line => line.Amount);
        });

        builder.Ignore(invoice => invoice.Total);
        builder.Ignore(invoice => invoice.IsPaid);

        builder.HasIndex(invoice => new { invoice.TenantId, invoice.Status });
    }
}

/// <summary>Mapeamento de <see cref="UsageQuota"/>.</summary>
internal sealed class UsageQuotaConfiguration : IEntityTypeConfiguration<UsageQuota>
{
    public void Configure(EntityTypeBuilder<UsageQuota> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("UsageQuotas");
        builder.HasKey(quota => quota.Id);
        builder.Property(quota => quota.Id).ValueGeneratedNever();
        builder.Property(quota => quota.TenantId).IsRequired();
        builder.Property(quota => quota.Resource).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(quota => quota.PeriodStart).IsRequired();
        builder.Property(quota => quota.PeriodEnd).IsRequired();
        builder.Property(quota => quota.Included).IsRequired();
        builder.Property(quota => quota.Consumed).IsRequired();

        // Consumo real de IA em tokens (entrada + saída), preservado com
        // precisão entre operações.
        builder.Property(quota => quota.ConsumedTokens).IsRequired();

        builder.Property(quota => quota.PurchasedExtra).IsRequired();
        builder.Property(quota => quota.TenantLimit);
        builder.Property(quota => quota.BillingMode).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Ignore(quota => quota.Available);
        builder.Ignore(quota => quota.Remaining);
        builder.Ignore(quota => quota.IsOverIncluded);
        builder.Ignore(quota => quota.AiTokenLimit);
        builder.Ignore(quota => quota.HasAiCapacity);

        // Unico por tenant, recurso e periodo: impede cota duplicada.
        builder.HasIndex(quota => new { quota.TenantId, quota.Resource, quota.PeriodStart }).IsUnique();
    }
}

/// <summary>Mapeamento de <see cref="CreditPurchase"/>.</summary>
internal sealed class CreditPurchaseConfiguration : IEntityTypeConfiguration<CreditPurchase>
{
    public void Configure(EntityTypeBuilder<CreditPurchase> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("CreditPurchases");
        builder.HasKey(purchase => purchase.Id);
        builder.Property(purchase => purchase.Id).ValueGeneratedNever();
        builder.Property(purchase => purchase.TenantId).IsRequired();
        builder.Property(purchase => purchase.Resource).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(purchase => purchase.Quantity).IsRequired();
        builder.Property(purchase => purchase.PurchasedAt).IsRequired();
        builder.Property(purchase => purchase.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(purchase => purchase.PlatformInvoiceId);

        builder.ComplexProperty(purchase => purchase.Amount, amount =>
        {
            amount.Property(money => money.Amount)
                .HasColumnName("Amount_Amount").HasPrecision(18, 2).IsRequired();
            amount.Property(money => money.Currency)
                .HasColumnName("Amount_Currency").HasMaxLength(3).IsRequired();
        });

        builder.Ignore(purchase => purchase.IsPaid);
        builder.HasIndex(purchase => new { purchase.TenantId, purchase.Status });
    }
}

/// <summary>Mapeamento de <see cref="SiteChangeCharge"/>.</summary>
internal sealed class SiteChangeChargeConfiguration : IEntityTypeConfiguration<SiteChangeCharge>
{
    public void Configure(EntityTypeBuilder<SiteChangeCharge> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("SiteChangeCharges");
        builder.HasKey(charge => charge.Id);
        builder.Property(charge => charge.Id).ValueGeneratedNever();
        builder.Property(charge => charge.TenantId).IsRequired();
        builder.Property(charge => charge.SiteId).IsRequired();
        builder.Property(charge => charge.RequestId).IsRequired();
        builder.Property(charge => charge.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(charge => charge.PlatformInvoiceId);

        builder.ComplexProperty(charge => charge.Amount, amount =>
        {
            amount.Property(money => money.Amount)
                .HasColumnName("Amount_Amount").HasPrecision(18, 2).IsRequired();
            amount.Property(money => money.Currency)
                .HasColumnName("Amount_Currency").HasMaxLength(3).IsRequired();
        });

        builder.HasIndex(charge => new { charge.TenantId, charge.Status });
    }
}
