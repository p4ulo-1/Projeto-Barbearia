using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servia.Domain.Tenancy;

namespace Servia.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeamento de <see cref="Plan"/>.
///
/// <see cref="Plan"/> é entidade <b>global</b> da plataforma: não implementa
/// <see cref="ITenantScoped"/> porque é compartilhada por todos os tenants.
/// </summary>
internal sealed class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Plans");

        builder.HasKey(plan => plan.Id);

        builder.Property(plan => plan.Id).ValueGeneratedNever();

        builder.Property(plan => plan.Code).HasMaxLength(30).IsRequired();

        builder.Property(plan => plan.Name).HasMaxLength(100).IsRequired();

        builder.Property(plan => plan.IncludedProfessionals).IsRequired();

        builder.Property(plan => plan.CustomizationLevel)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(plan => plan.IncludedAiUnits).IsRequired();

        builder.Property(plan => plan.IsActive).IsRequired();

        builder.ComplexProperty(plan => plan.MonthlyPrice, price =>
        {
            price.Property(money => money.Amount)
                .HasColumnName("MonthlyPrice_Amount")
                .HasPrecision(18, 2)
                .IsRequired();

            price.Property(money => money.Currency)
                .HasColumnName("MonthlyPrice_Currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.HasIndex(plan => plan.Code).IsUnique();

        builder.Ignore(plan => plan.HasUnlimitedProfessionals);
    }
}
