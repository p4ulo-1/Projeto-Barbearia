using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servia.Domain.Catalog;

namespace Servia.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeamento de <see cref="Service"/>.
///
/// <c>Money</c> é um objeto de valor implementado como <c>readonly record struct</c>.
/// Por ser um tipo por valor, é mapeado com <c>ComplexProperty</c> — e não com
/// <c>OwnsOne</c>, que exige uma classe. As colunas ficam na própria tabela.
/// </summary>
internal sealed class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Services");

        builder.HasKey(service => service.Id);

        // Chave alternativa (TenantId, Id): alvo das FKs compostas do agendamento.
        builder.HasAlternateKey(service => new { service.TenantId, service.Id });

        builder.Property(service => service.Id).ValueGeneratedNever();

        builder.Property(service => service.TenantId).IsRequired();

        builder.Property(service => service.Name).HasMaxLength(150).IsRequired();

        builder.Property(service => service.Description).HasMaxLength(1000);

        builder.Property(service => service.DurationMinutes).IsRequired();

        builder.Property(service => service.Highlight).IsRequired();

        builder.Property(service => service.IsActive).IsRequired();

        builder.ComplexProperty(service => service.Price, price =>
        {
            price.Property(money => money.Amount)
                .HasColumnName("Price_Amount")
                .HasPrecision(18, 2)
                .IsRequired();

            price.Property(money => money.Currency)
                .HasColumnName("Price_Currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.HasIndex(service => new { service.TenantId, service.IsActive });
    }
}
