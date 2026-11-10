using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servia.Domain.Catalog;

namespace Servia.Infrastructure.Persistence.Configurations;

/// <summary>Mapeamento de <see cref="ServiceCategory"/>.</summary>
internal sealed class ServiceCategoryConfiguration : IEntityTypeConfiguration<ServiceCategory>
{
    public void Configure(EntityTypeBuilder<ServiceCategory> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ServiceCategories");
        builder.HasKey(category => category.Id);
        builder.Property(category => category.Id).ValueGeneratedNever();
        builder.Property(category => category.TenantId).IsRequired();
        builder.Property(category => category.Name).HasMaxLength(100).IsRequired();
        builder.Property(category => category.SortOrder).IsRequired();

        builder.HasIndex(category => new { category.TenantId, category.SortOrder });
    }
}

/// <summary>Mapeamento de <see cref="ServiceVariant"/>.</summary>
internal sealed class ServiceVariantConfiguration : IEntityTypeConfiguration<ServiceVariant>
{
    public void Configure(EntityTypeBuilder<ServiceVariant> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ServiceVariants");
        builder.HasKey(variant => variant.Id);
        builder.Property(variant => variant.Id).ValueGeneratedNever();
        builder.Property(variant => variant.TenantId).IsRequired();
        builder.Property(variant => variant.ServiceId).IsRequired();
        builder.Property(variant => variant.Label).HasMaxLength(100).IsRequired();
        builder.Property(variant => variant.DurationMinutes).IsRequired();

        builder.ComplexProperty(variant => variant.Price, price =>
        {
            price.Property(money => money.Amount)
                .HasColumnName("Price_Amount").HasPrecision(18, 2).IsRequired();
            price.Property(money => money.Currency)
                .HasColumnName("Price_Currency").HasMaxLength(3).IsRequired();
        });

        builder.HasIndex(variant => new { variant.TenantId, variant.ServiceId });
    }
}

/// <summary>Mapeamento de <see cref="ServiceCombo"/> e seus itens.</summary>
internal sealed class ServiceComboConfiguration : IEntityTypeConfiguration<ServiceCombo>
{
    public void Configure(EntityTypeBuilder<ServiceCombo> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ServiceCombos");
        builder.HasKey(combo => combo.Id);
        builder.Property(combo => combo.Id).ValueGeneratedNever();
        builder.Property(combo => combo.TenantId).IsRequired();
        builder.Property(combo => combo.Name).HasMaxLength(150).IsRequired();

        builder.ComplexProperty(combo => combo.ComboPrice, price =>
        {
            price.Property(money => money.Amount)
                .HasColumnName("ComboPrice_Amount").HasPrecision(18, 2).IsRequired();
            price.Property(money => money.Currency)
                .HasColumnName("ComboPrice_Currency").HasMaxLength(3).IsRequired();
        });

        // Itens do combo: owned collection, sem tabela propria de agregado.
        builder.OwnsMany(combo => combo.Items, items =>
        {
            items.ToTable("ServiceComboItems");
            items.WithOwner().HasForeignKey("ServiceComboId");
            items.Property(item => item.ServiceId).IsRequired();
            items.Property(item => item.Quantity).IsRequired();
            items.HasKey("ServiceComboId", nameof(ComboItem.ServiceId));
        });

        builder.HasIndex(combo => combo.TenantId);
    }
}
