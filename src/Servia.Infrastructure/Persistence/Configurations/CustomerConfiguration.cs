using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servia.Domain.Customers;

namespace Servia.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeamento de <see cref="Customer"/>.
///
/// O índice <c>(TenantId, Phone)</c> atende à busca por telefone no
/// atendimento (ARCH.md §6.2). O cadastro é específico do tenant: a mesma
/// pessoa em dois estabelecimentos gera dois registros independentes
/// (ADR-015).
/// </summary>
internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Customers");

        builder.HasKey(customer => customer.Id);

        builder.Property(customer => customer.Id).ValueGeneratedNever();

        builder.Property(customer => customer.TenantId).IsRequired();

        builder.Property(customer => customer.UserId);

        builder.Property(customer => customer.Name).HasMaxLength(150).IsRequired();

        builder.Property(customer => customer.Phone).HasMaxLength(20).IsRequired();

        builder.Property(customer => customer.Email).HasMaxLength(200);

        builder.Property(customer => customer.BirthDate);

        builder.Property(customer => customer.IsActive).IsRequired();

        builder.Property(customer => customer.CreatedAt).IsRequired();

        // Índice crítico: busca por telefone no atendimento.
        builder.HasIndex(customer => new { customer.TenantId, customer.Phone });

        builder.Ignore(customer => customer.IsAnonymized);
    }
}
