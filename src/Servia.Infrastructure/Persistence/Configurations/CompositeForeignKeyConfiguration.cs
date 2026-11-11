using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servia.Domain.Appointments;
using Servia.Domain.Customers;
using Servia.Domain.Finance;
using Servia.Domain.Loyalty;
using Servia.Domain.Memberships;
using Servia.Domain.Team;

namespace Servia.Infrastructure.Persistence.Configurations;

/// <summary>
/// Chaves estrangeiras compostas <c>(TenantId, Id)</c> (Etapa 4 da auditoria).
///
/// <b>Por que compostas:</b> uma FK simples em <c>CustomerId</c> aceitaria
/// um cliente de qualquer tenant. A FK composta obriga que o registro
/// referenciado pertença ao mesmo tenant — o banco passa a impedir
/// associação entre estabelecimentos, independentemente da aplicação.
///
/// <b>Entidades globais:</b> <c>Plan</c> não é escopada por tenant, então
/// seus relacionamentos usam FK simples. Aplicar <c>(TenantId, Id)</c> a
/// uma entidade global quebraria o modelo.
///
/// Cada configuração aqui adiciona apenas o relacionamento; o mapeamento
/// de colunas permanece nas configurações originais.
/// </summary>
internal sealed class CompositeForeignKeyConfiguration :
    IEntityTypeConfiguration<Membership>,
    IEntityTypeConfiguration<ServiceReview>,
    IEntityTypeConfiguration<CommissionEntry>,
    IEntityTypeConfiguration<Payment>,
    IEntityTypeConfiguration<LoyaltyAccount>,
    IEntityTypeConfiguration<PackageUsage>,
    IEntityTypeConfiguration<ProfessionalService>,
    IEntityTypeConfiguration<ScheduleBlock>,
    IEntityTypeConfiguration<WaitlistEntry>,
    IEntityTypeConfiguration<CustomerPreference>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Cliente do mesmo tenant.
        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(membership => new { membership.TenantId, membership.CustomerId })
            .HasPrincipalKey(customer => new { customer.TenantId, customer.Id })
            .OnDelete(DeleteBehavior.Restrict);

        // Clube do mesmo tenant.
        builder.HasOne<MembershipPlan>()
            .WithMany()
            .HasForeignKey(membership => new { membership.TenantId, membership.MembershipPlanId })
            .HasPrincipalKey(plan => new { plan.TenantId, plan.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<ServiceReview> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasOne<Domain.Appointments.Appointment>()
            .WithMany()
            .HasForeignKey(review => new { review.TenantId, review.AppointmentId })
            .HasPrincipalKey(appointment => new { appointment.TenantId, appointment.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(review => new { review.TenantId, review.CustomerId })
            .HasPrincipalKey(customer => new { customer.TenantId, customer.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<CommissionEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasOne<Professional>()
            .WithMany()
            .HasForeignKey(entry => new { entry.TenantId, entry.ProfessionalId })
            .HasPrincipalKey(professional => new { professional.TenantId, professional.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Appointments.Appointment>()
            .WithMany()
            .HasForeignKey(entry => new { entry.TenantId, entry.AppointmentId })
            .HasPrincipalKey(appointment => new { appointment.TenantId, appointment.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // AppointmentId e opcional: pagamento avulso nao referencia agendamento.
        builder.HasOne<Domain.Appointments.Appointment>()
            .WithMany()
            .HasForeignKey(payment => new { payment.TenantId, payment.AppointmentId })
            .HasPrincipalKey(appointment => new { appointment.TenantId, appointment.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<LoyaltyAccount> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(account => new { account.TenantId, account.CustomerId })
            .HasPrincipalKey(customer => new { customer.TenantId, customer.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<PackageUsage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasOne<ServicePackage>()
            .WithMany()
            .HasForeignKey(usage => new { usage.TenantId, usage.PackageId })
            .HasPrincipalKey(package => new { package.TenantId, package.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(usage => new { usage.TenantId, usage.CustomerId })
            .HasPrincipalKey(customer => new { customer.TenantId, customer.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<ProfessionalService> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasOne<Professional>()
            .WithMany()
            .HasForeignKey(link => new { link.TenantId, link.ProfessionalId })
            .HasPrincipalKey(professional => new { professional.TenantId, professional.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Domain.Catalog.Service>()
            .WithMany()
            .HasForeignKey(link => new { link.TenantId, link.ServiceId })
            .HasPrincipalKey(service => new { service.TenantId, service.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<ScheduleBlock> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasOne<Professional>()
            .WithMany()
            .HasForeignKey(block => new { block.TenantId, block.ProfessionalId })
            .HasPrincipalKey(professional => new { professional.TenantId, professional.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<WaitlistEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(entry => new { entry.TenantId, entry.CustomerId })
            .HasPrincipalKey(customer => new { customer.TenantId, customer.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Domain.Catalog.Service>()
            .WithMany()
            .HasForeignKey(entry => new { entry.TenantId, entry.ServiceId })
            .HasPrincipalKey(service => new { service.TenantId, service.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Professional>()
            .WithMany()
            .HasForeignKey(entry => new { entry.TenantId, entry.ProfessionalId })
            .HasPrincipalKey(professional => new { professional.TenantId, professional.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<CustomerPreference> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(preference => new { preference.TenantId, preference.CustomerId })
            .HasPrincipalKey(customer => new { customer.TenantId, customer.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
