using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servia.Domain.Finance;

namespace Servia.Infrastructure.Persistence.Configurations;

/// <summary>Mapeamento de <see cref="Payment"/>.</summary>
internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Payments");
        builder.HasKey(payment => payment.Id);
        builder.Property(payment => payment.Id).ValueGeneratedNever();
        builder.Property(payment => payment.TenantId).IsRequired();
        builder.Property(payment => payment.AppointmentId);
        builder.Property(payment => payment.Method).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(payment => payment.Status).HasConversion<string>().HasMaxLength(25).IsRequired();
        builder.Property(payment => payment.GatewayPaymentId).HasMaxLength(100);
        builder.Property(payment => payment.CreatedAt).IsRequired();
        builder.Property(payment => payment.PaidAt);

        builder.ComplexProperty(payment => payment.Amount, amount =>
        {
            amount.Property(money => money.Amount)
                .HasColumnName("Amount_Amount").HasPrecision(18, 2).IsRequired();
            amount.Property(money => money.Currency)
                .HasColumnName("Amount_Currency").HasMaxLength(3).IsRequired();
        });

        builder.ComplexProperty(payment => payment.RefundedAmount, refunded =>
        {
            refunded.Property(money => money.Amount)
                .HasColumnName("Refunded_Amount").HasPrecision(18, 2).IsRequired();
            refunded.Property(money => money.Currency)
                .HasColumnName("Refunded_Currency").HasMaxLength(3).IsRequired();
        });

        builder.Ignore(payment => payment.IsPaid);
        builder.Ignore(payment => payment.NetAmount);

        builder.HasIndex(payment => new { payment.TenantId, payment.Status });
        builder.HasIndex(payment => new { payment.TenantId, payment.AppointmentId });
    }
}

/// <summary>Mapeamento de <see cref="Transaction"/>.</summary>
internal sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Transactions");
        builder.HasKey(transaction => transaction.Id);
        builder.Property(transaction => transaction.Id).ValueGeneratedNever();
        builder.Property(transaction => transaction.TenantId).IsRequired();
        builder.Property(transaction => transaction.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(transaction => transaction.CategoryId);
        builder.Property(transaction => transaction.Description).HasMaxLength(300).IsRequired();
        builder.Property(transaction => transaction.OccurredAt).IsRequired();
        builder.Property(transaction => transaction.PaymentId);

        builder.ComplexProperty(transaction => transaction.Amount, amount =>
        {
            amount.Property(money => money.Amount)
                .HasColumnName("Amount_Amount").HasPrecision(18, 2).IsRequired();
            amount.Property(money => money.Currency)
                .HasColumnName("Amount_Currency").HasMaxLength(3).IsRequired();
        });

        builder.Ignore(transaction => transaction.IsIncome);

        // Consulta de relatorio por periodo.
        builder.HasIndex(transaction => new { transaction.TenantId, transaction.OccurredAt });
    }
}

/// <summary>Mapeamento de <see cref="CommissionRule"/>.</summary>
internal sealed class CommissionRuleConfiguration : IEntityTypeConfiguration<CommissionRule>
{
    public void Configure(EntityTypeBuilder<CommissionRule> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("CommissionRules");
        builder.HasKey(rule => rule.Id);
        builder.Property(rule => rule.Id).ValueGeneratedNever();
        builder.Property(rule => rule.TenantId).IsRequired();
        builder.Property(rule => rule.ProfessionalId);
        builder.Property(rule => rule.ServiceId);
        builder.Property(rule => rule.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(rule => rule.Percentage).HasPrecision(5, 2).IsRequired();

        builder.ComplexProperty(rule => rule.FixedAmount, fixedAmount =>
        {
            fixedAmount.Property(money => money.Amount)
                .HasColumnName("FixedAmount_Amount").HasPrecision(18, 2);
            fixedAmount.Property(money => money.Currency)
                .HasColumnName("FixedAmount_Currency").HasMaxLength(3);
        });

        builder.ComplexProperty(rule => rule.TargetAmount, target =>
        {
            target.Property(money => money.Amount)
                .HasColumnName("TargetAmount_Amount").HasPrecision(18, 2);
            target.Property(money => money.Currency)
                .HasColumnName("TargetAmount_Currency").HasMaxLength(3);
        });

        builder.Ignore(rule => rule.Specificity);
        builder.HasIndex(rule => new { rule.TenantId, rule.ProfessionalId });
    }
}

/// <summary>Mapeamento de <see cref="CommissionEntry"/>.</summary>
internal sealed class CommissionEntryConfiguration : IEntityTypeConfiguration<CommissionEntry>
{
    public void Configure(EntityTypeBuilder<CommissionEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("CommissionEntries");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).ValueGeneratedNever();
        builder.Property(entry => entry.TenantId).IsRequired();
        builder.Property(entry => entry.ProfessionalId).IsRequired();
        builder.Property(entry => entry.AppointmentId).IsRequired();
        builder.Property(entry => entry.ReferencePeriod).IsRequired();
        builder.Property(entry => entry.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(entry => entry.CreatedAt).IsRequired();
        builder.Property(entry => entry.PaidAt);

        builder.ComplexProperty(entry => entry.Amount, amount =>
        {
            amount.Property(money => money.Amount)
                .HasColumnName("Amount_Amount").HasPrecision(18, 2).IsRequired();
            amount.Property(money => money.Currency)
                .HasColumnName("Amount_Currency").HasMaxLength(3).IsRequired();
        });

        builder.Ignore(entry => entry.IsPaid);

        // Extrato do profissional por periodo.
        builder.HasIndex(entry => new { entry.TenantId, entry.ProfessionalId, entry.ReferencePeriod });
    }
}
