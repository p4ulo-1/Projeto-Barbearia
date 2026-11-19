using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servia.Domain.Operations;

namespace Servia.Infrastructure.Persistence.Configurations;

internal sealed class MercadoPagoNotificationConfiguration : IEntityTypeConfiguration<MercadoPagoNotification>
{
    public void Configure(EntityTypeBuilder<MercadoPagoNotification> builder)
    {
        builder.ToTable("MercadoPagoInbox");
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.Topic).HasMaxLength(80);
        builder.Property(item => item.ExternalId).HasMaxLength(100);
        builder.Property(item => item.ResourceId).HasMaxLength(100);
        builder.Property(item => item.RequestId).HasMaxLength(128);
        builder.Property(item => item.Result).HasMaxLength(40);
        builder.HasIndex(item => new { item.Topic, item.ExternalId, item.ResourceId }).IsUnique();
        builder.HasIndex(item => new { item.RequestId, item.ResourceId, item.SignatureTimestamp }).IsUnique();
        builder.HasIndex(item => new { item.AvailableAt, item.ReceivedAt }).HasFilter("\"CompletedAt\" IS NULL");
    }
}
