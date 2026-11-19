using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servia.Domain.Billing;
using Servia.Domain.Tenancy;

namespace Servia.Infrastructure.Persistence.Configurations;

internal sealed class SubscriptionReconciliationConfiguration : IEntityTypeConfiguration<SubscriptionReconciliation>
{
    public void Configure(EntityTypeBuilder<SubscriptionReconciliation> builder)
    {
        builder.ToTable("SubscriptionReconciliation");
        builder.HasKey(item => item.SubscriptionId);
        builder.Property(item => item.SubscriptionId).ValueGeneratedNever();
        builder.Property(item => item.Result).HasMaxLength(40);
        builder.HasOne<Subscription>().WithOne().HasForeignKey<SubscriptionReconciliation>(item => item.SubscriptionId);
        builder.HasIndex(item => item.NextCheckAt);
        builder.HasIndex(item => item.RateLimitedUntil).HasFilter("\"RateLimitedUntil\" IS NOT NULL");
    }
}
