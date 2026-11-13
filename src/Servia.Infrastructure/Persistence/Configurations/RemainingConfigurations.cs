using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servia.Domain.Ai;
using Servia.Domain.Appointments;
using Servia.Domain.Customers;
using Servia.Domain.Loyalty;
using Servia.Domain.Memberships;
using Servia.Domain.Operations;
using Servia.Domain.Sites;
using Servia.Domain.Team;

namespace Servia.Infrastructure.Persistence.Configurations;

/// <summary>Mapeamento de <see cref="ProfessionalService"/>.</summary>
internal sealed class ProfessionalServiceConfiguration : IEntityTypeConfiguration<ProfessionalService>
{
    public void Configure(EntityTypeBuilder<ProfessionalService> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ProfessionalServices");
        builder.HasKey(link => new { link.TenantId, link.ProfessionalId, link.ServiceId });
        builder.Property(link => link.TenantId).IsRequired();
        builder.Property(link => link.ProfessionalId).IsRequired();
        builder.Property(link => link.ServiceId).IsRequired();

        builder.HasIndex(link => new { link.TenantId, link.ServiceId });
    }
}

/// <summary>Mapeamento de <see cref="ScheduleBlock"/>.</summary>
internal sealed class ScheduleBlockConfiguration : IEntityTypeConfiguration<ScheduleBlock>
{
    public void Configure(EntityTypeBuilder<ScheduleBlock> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ScheduleBlocks");
        builder.HasKey(block => block.Id);
        builder.Property(block => block.Id).ValueGeneratedNever();
        builder.Property(block => block.TenantId).IsRequired();
        builder.Property(block => block.ProfessionalId).IsRequired();
        builder.Property(block => block.Start).IsRequired();
        builder.Property(block => block.End).IsRequired();
        builder.Property(block => block.Reason).HasMaxLength(300);

        builder.HasIndex(block => new { block.TenantId, block.ProfessionalId, block.Start });
    }
}

/// <summary>Mapeamento de <see cref="CustomerPreference"/>.</summary>
internal sealed class CustomerPreferenceConfiguration : IEntityTypeConfiguration<CustomerPreference>
{
    public void Configure(EntityTypeBuilder<CustomerPreference> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("CustomerPreferences");
        builder.HasKey(preference => preference.CustomerId);
        builder.Property(preference => preference.TenantId).IsRequired();
        builder.Property(preference => preference.CustomerId).IsRequired();
        builder.Property(preference => preference.PreferredProfessionalId);
        builder.Property(preference => preference.Notes).HasMaxLength(1000);
    }
}

/// <summary>Mapeamento de <see cref="WaitlistEntry"/>.</summary>
internal sealed class WaitlistEntryConfiguration : IEntityTypeConfiguration<WaitlistEntry>
{
    public void Configure(EntityTypeBuilder<WaitlistEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("WaitlistEntries");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).ValueGeneratedNever();
        builder.Property(entry => entry.TenantId).IsRequired();
        builder.Property(entry => entry.CustomerId).IsRequired();
        builder.Property(entry => entry.ServiceId).IsRequired();
        builder.Property(entry => entry.ProfessionalId).IsRequired();
        builder.Property(entry => entry.PreferredDate).IsRequired();
        builder.Property(entry => entry.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(entry => entry.OfferedTime);
        builder.Property(entry => entry.OfferExpiresAt);
        builder.Property(entry => entry.CreatedAt).IsRequired();

        builder.Ignore(entry => entry.IsWaiting);

        builder.HasIndex(entry => new { entry.TenantId, entry.Status, entry.PreferredDate });
    }
}

/// <summary>Mapeamento de <see cref="ScheduleLock"/>.</summary>
internal sealed class ScheduleLockConfiguration : IEntityTypeConfiguration<ScheduleLock>
{
    public void Configure(EntityTypeBuilder<ScheduleLock> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ScheduleLocks");
        builder.HasKey(lockEntry => lockEntry.Id);
        builder.Property(lockEntry => lockEntry.Id).ValueGeneratedNever();
        builder.Property(lockEntry => lockEntry.TenantId).IsRequired();
        builder.Property(lockEntry => lockEntry.ProfessionalId).IsRequired();
        builder.Property(lockEntry => lockEntry.Date).IsRequired();
        builder.Property(lockEntry => lockEntry.StartTime).IsRequired();
        builder.Property(lockEntry => lockEntry.EndTime).IsRequired();
        builder.Property(lockEntry => lockEntry.AppointmentId);
        builder.Property(lockEntry => lockEntry.ExpiresAt).IsRequired();



        // Indice unico PARCIAL: impede reserva concorrente do mesmo horario
        // enquanto a trava esta ativa.
        //
        // O filtro usa a coluna IsActive, nao now(): o PostgreSQL exige que
        // predicados de indice sejam IMMUTABLE, e now() nao e. A trava e
        // marcada inativa quando expira ou e liberada.
        builder.HasIndex(lockEntry => new
        {
            lockEntry.TenantId,
            lockEntry.ProfessionalId,
            lockEntry.Date,
            lockEntry.StartTime
        })
            .IsUnique()
            .HasFilter("\"IsActive\"");
    }
}

/// <summary>Mapeamento de <see cref="Site"/>.</summary>
internal sealed class SiteConfiguration : IEntityTypeConfiguration<Site>
{
    public void Configure(EntityTypeBuilder<Site> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Sites");
        builder.HasKey(site => site.Id);
        builder.Property(site => site.Id).ValueGeneratedNever();
        builder.Property(site => site.TenantId).IsRequired();
        builder.Property(site => site.Subdomain).HasMaxLength(40).IsRequired();

        // C10/identidade: o dominio proprio e armazenado em minusculas. O
        // resolvedor publico compara o host normalizado (minusculo), entao
        // normalizar na escrita evita divergencia por caixa. O conversor
        // garante consistencia independente do que o escritor enviar.
        builder.Property(site => site.CustomDomain)
            .HasMaxLength(200)
            .HasConversion(
                value => value == null ? null : value.Trim().ToLowerInvariant(),
                value => value);

        builder.Property(site => site.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(site => site.PublishedVersionId);

        builder.ComplexProperty(site => site.Theme, theme =>
        {
            theme.Property(value => value.PrimaryColor).HasColumnName("Theme_Primary").HasMaxLength(7).IsRequired();
            theme.Property(value => value.SecondaryColor).HasColumnName("Theme_Secondary").HasMaxLength(7).IsRequired();
            theme.Property(value => value.FontFamily).HasColumnName("Theme_Font").HasMaxLength(100).IsRequired();
            theme.Property(value => value.LayoutStyle).HasColumnName("Theme_Layout").HasMaxLength(50).IsRequired();
        });

        builder.OwnsMany(site => site.Pages, pages =>
        {
            pages.ToTable("SitePages");
            pages.WithOwner().HasForeignKey("SiteId");
            pages.Property(page => page.Id).ValueGeneratedNever();
            pages.Property(page => page.Slug).HasMaxLength(100).IsRequired();
            pages.Property(page => page.Title).HasMaxLength(150).IsRequired();
            pages.Property(page => page.SortOrder).IsRequired();
            pages.HasKey(page => page.Id);

            pages.OwnsMany(page => page.Sections, sections =>
            {
                sections.ToTable("SiteSections");
                sections.WithOwner().HasForeignKey("PageId");
                sections.Property(section => section.Id).ValueGeneratedNever();
                sections.Property(section => section.ComponentType).HasConversion<string>().HasMaxLength(30).IsRequired();
                sections.Property(section => section.SortOrder).IsRequired();
                sections.Property(section => section.Configuration)
                    .HasConversion(
                        config => System.Text.Json.JsonSerializer.Serialize(config, (System.Text.Json.JsonSerializerOptions?)null),
                        json => DeserializeConfiguration(json))
                    .HasColumnName("Configuration")
                    .IsRequired();
                sections.HasKey(section => section.Id);
            });
        });

        builder.OwnsMany(site => site.Publications, publications =>
        {
            publications.ToTable("SitePublications");
            publications.WithOwner().HasForeignKey("SiteId");
            publications.Property(publication => publication.Id).ValueGeneratedNever();
            publications.Property(publication => publication.Version).IsRequired();
            publications.Property(publication => publication.Snapshot).IsRequired();
            publications.Property(publication => publication.PublishedBy).IsRequired();
            publications.Property(publication => publication.PublishedAt).IsRequired();
            publications.HasKey(publication => publication.Id);
        });

        builder.Ignore(site => site.IsPublished);

        // Subdominio unico: e o endereco do site.
        builder.HasIndex(site => site.Subdomain).IsUnique();

        // Dominio proprio unico quando informado (lacuna da auditoria). O
        // filtro exclui NULL, permitindo varios sites sem dominio proprio.
        // Como o valor e gravado em minusculas (conversor), a unicidade
        // tambem vale para variacoes de caixa.
        builder.HasIndex(site => site.CustomDomain)
            .IsUnique()
            .HasFilter("\"CustomDomain\" IS NOT NULL");

        // C10: concorrencia otimista no agregado Site. `Version` e um inteiro
        // real, incrementado a cada publicacao; o EF inclui o valor original
        // no WHERE do UPDATE, abortando a publicacao concorrente.
        builder.Property(site => site.Version)
            .IsConcurrencyToken()
            .IsRequired();
    }

    private static IReadOnlyDictionary<string, string> DeserializeConfiguration(string json) =>
        System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
}

/// <summary>Mapeamento de <see cref="MembershipPlan"/>.</summary>
internal sealed class MembershipPlanConfiguration : IEntityTypeConfiguration<MembershipPlan>
{
    public void Configure(EntityTypeBuilder<MembershipPlan> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("MembershipPlans");
        builder.HasKey(plan => plan.Id);
        builder.Property(plan => plan.Id).ValueGeneratedNever();
        builder.Property(plan => plan.TenantId).IsRequired();
        builder.Property(plan => plan.Name).HasMaxLength(150).IsRequired();
        builder.Property(plan => plan.IsActive).IsRequired();

        builder.ComplexProperty(plan => plan.MonthlyPrice, price =>
        {
            price.Property(money => money.Amount)
                .HasColumnName("MonthlyPrice_Amount").HasPrecision(18, 2).IsRequired();
            price.Property(money => money.Currency)
                .HasColumnName("MonthlyPrice_Currency").HasMaxLength(3).IsRequired();
        });

        builder.OwnsMany(plan => plan.Benefits, benefits =>
        {
            benefits.ToTable("MembershipBenefits");
            benefits.WithOwner().HasForeignKey("MembershipPlanId");
            benefits.Property(benefit => benefit.ServiceId);
            benefits.Property(benefit => benefit.QuantityPerPeriod).IsRequired();
            benefits.Property(benefit => benefit.DiscountPercentage).HasPrecision(5, 2);
            benefits.HasKey("MembershipPlanId", nameof(MembershipBenefit.ServiceId));
        });

        builder.HasIndex(plan => new { plan.TenantId, plan.IsActive });
    }
}

/// <summary>Mapeamento de <see cref="Membership"/>.</summary>
internal sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Memberships");
        builder.HasKey(membership => membership.Id);
        builder.Property(membership => membership.Id).ValueGeneratedNever();
        builder.Property(membership => membership.TenantId).IsRequired();
        builder.Property(membership => membership.CustomerId).IsRequired();
        builder.Property(membership => membership.MembershipPlanId).IsRequired();
        builder.Property(membership => membership.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(membership => membership.CurrentPeriodStart).IsRequired();
        builder.Property(membership => membership.CurrentPeriodEnd).IsRequired();

        // Consumo por servico: persistido como JSON.
        builder.Property(membership => membership.ConsumedByService)
            .HasConversion(
                consumed => System.Text.Json.JsonSerializer.Serialize(consumed, (System.Text.Json.JsonSerializerOptions?)null),
                json => DeserializeConsumption(json))
            .HasColumnName("ConsumedByService")
            .IsRequired();

        builder.Ignore(membership => membership.IsActive);
        builder.Ignore(membership => membership.IsSuspended);

        builder.HasIndex(membership => new { membership.TenantId, membership.CustomerId });
    }

    private static IReadOnlyDictionary<Guid, int> DeserializeConsumption(string json) =>
        System.Text.Json.JsonSerializer.Deserialize<Dictionary<Guid, int>>(json) ?? [];
}

/// <summary>Mapeamento de <see cref="ServicePackage"/>.</summary>
internal sealed class ServicePackageConfiguration : IEntityTypeConfiguration<ServicePackage>
{
    public void Configure(EntityTypeBuilder<ServicePackage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ServicePackages");
        builder.HasKey(package => package.Id);
        builder.Property(package => package.Id).ValueGeneratedNever();
        builder.Property(package => package.TenantId).IsRequired();
        builder.Property(package => package.Name).HasMaxLength(150).IsRequired();
        builder.Property(package => package.SessionsIncluded).IsRequired();
        builder.Property(package => package.ValidityDays).IsRequired();
        builder.Property(package => package.IsActive).IsRequired();

        builder.ComplexProperty(package => package.Price, price =>
        {
            price.Property(money => money.Amount)
                .HasColumnName("Price_Amount").HasPrecision(18, 2).IsRequired();
            price.Property(money => money.Currency)
                .HasColumnName("Price_Currency").HasMaxLength(3).IsRequired();
        });

        builder.HasIndex(package => new { package.TenantId, package.IsActive });
    }
}

/// <summary>Mapeamento de <see cref="PackageUsage"/>.</summary>
internal sealed class PackageUsageConfiguration : IEntityTypeConfiguration<PackageUsage>
{
    public void Configure(EntityTypeBuilder<PackageUsage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("PackageUsages");
        builder.HasKey(usage => usage.Id);
        builder.Property(usage => usage.Id).ValueGeneratedNever();
        builder.Property(usage => usage.TenantId).IsRequired();
        builder.Property(usage => usage.PackageId).IsRequired();
        builder.Property(usage => usage.CustomerId).IsRequired();
        builder.Property(usage => usage.SessionsRemaining).IsRequired();
        builder.Property(usage => usage.ExpiresAt).IsRequired();

        builder.Ignore(usage => usage.IsExhausted);

        builder.HasIndex(usage => new { usage.TenantId, usage.CustomerId });
    }
}

/// <summary>Mapeamento de <see cref="LoyaltyAccount"/>.</summary>
internal sealed class LoyaltyAccountConfiguration : IEntityTypeConfiguration<LoyaltyAccount>
{
    public void Configure(EntityTypeBuilder<LoyaltyAccount> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("LoyaltyAccounts");
        builder.HasKey(account => account.Id);
        builder.Property(account => account.Id).ValueGeneratedNever();
        builder.Property(account => account.TenantId).IsRequired();
        builder.Property(account => account.CustomerId).IsRequired();
        builder.Property(account => account.Points).IsRequired();
        builder.Property(account => account.TotalEarned).IsRequired();
        builder.Property(account => account.TotalRedeemed).IsRequired();

        // Uma conta por cliente em cada tenant.
        builder.HasIndex(account => new { account.TenantId, account.CustomerId }).IsUnique();
    }
}

/// <summary>Mapeamento de <see cref="ReactivationCampaign"/>.</summary>
internal sealed class ReactivationCampaignConfiguration : IEntityTypeConfiguration<ReactivationCampaign>
{
    public void Configure(EntityTypeBuilder<ReactivationCampaign> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ReactivationCampaigns");
        builder.HasKey(campaign => campaign.Id);
        builder.Property(campaign => campaign.Id).ValueGeneratedNever();
        builder.Property(campaign => campaign.TenantId).IsRequired();
        builder.Property(campaign => campaign.Name).HasMaxLength(150).IsRequired();
        builder.Property(campaign => campaign.InactiveDays).IsRequired();
        builder.Property(campaign => campaign.IsActive).IsRequired();

        builder.HasIndex(campaign => new { campaign.TenantId, campaign.IsActive });
    }
}

/// <summary>Mapeamento de <see cref="ServiceReview"/>.</summary>
internal sealed class ServiceReviewConfiguration : IEntityTypeConfiguration<ServiceReview>
{
    public void Configure(EntityTypeBuilder<ServiceReview> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ServiceReviews");
        builder.HasKey(review => review.Id);
        builder.Property(review => review.Id).ValueGeneratedNever();
        builder.Property(review => review.TenantId).IsRequired();
        builder.Property(review => review.AppointmentId).IsRequired();
        builder.Property(review => review.CustomerId).IsRequired();
        builder.Property(review => review.Score).IsRequired();
        builder.Property(review => review.Comment).HasMaxLength(1000);
        builder.Property(review => review.CreatedAt).IsRequired();

        builder.Ignore(review => review.Category);

        // Uma avaliacao por atendimento: evita distorcer o NPS.
        builder.HasIndex(review => new { review.TenantId, review.AppointmentId }).IsUnique();
    }
}

/// <summary>Mapeamento de <see cref="AiOperationLog"/>.</summary>
internal sealed class AiOperationLogConfiguration : IEntityTypeConfiguration<AiOperationLog>
{
    public void Configure(EntityTypeBuilder<AiOperationLog> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AiOperationLogs");
        builder.HasKey(log => log.Id);
        builder.Property(log => log.Id).ValueGeneratedNever();
        builder.Property(log => log.TenantId).IsRequired();
        builder.Property(log => log.OperationType).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(log => log.ProviderName).HasMaxLength(100).IsRequired();
        builder.Property(log => log.UnitsConsumed).IsRequired();
        builder.Property(log => log.Success).IsRequired();
        builder.Property(log => log.FailureReason).HasMaxLength(300);
        builder.Property(log => log.OccurredAt).IsRequired();

        builder.HasIndex(log => new { log.TenantId, log.OccurredAt });
    }
}

/// <summary>Mapeamento de <see cref="DataSubjectRequest"/>.</summary>
internal sealed class DataSubjectRequestConfiguration : IEntityTypeConfiguration<DataSubjectRequest>
{
    public void Configure(EntityTypeBuilder<DataSubjectRequest> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("DataSubjectRequests");
        builder.HasKey(request => request.Id);
        builder.Property(request => request.Id).ValueGeneratedNever();
        builder.Property(request => request.TenantId).IsRequired();
        builder.Property(request => request.CustomerId).IsRequired();
        builder.Property(request => request.RequestType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(request => request.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(request => request.RequestedAt).IsRequired();
        builder.Property(request => request.CompletedAt);

        builder.Ignore(request => request.DueAt);

        builder.HasIndex(request => new { request.TenantId, request.Status });
    }
}

/// <summary>Mapeamento de <see cref="TenantBackup"/>.</summary>
internal sealed class TenantBackupConfiguration : IEntityTypeConfiguration<TenantBackup>
{
    public void Configure(EntityTypeBuilder<TenantBackup> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("TenantBackups");
        builder.HasKey(backup => backup.Id);
        builder.Property(backup => backup.Id).ValueGeneratedNever();
        builder.Property(backup => backup.TenantId).IsRequired();
        builder.Property(backup => backup.StorageLocation).HasMaxLength(500).IsRequired();
        builder.Property(backup => backup.SizeInBytes).IsRequired();
        builder.Property(backup => backup.Checksum).HasMaxLength(200).IsRequired();
        builder.Property(backup => backup.CreatedAt).IsRequired();
        builder.Property(backup => backup.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(backup => backup.RestoredAt);

        builder.Ignore(backup => backup.CanRestore);

        builder.HasIndex(backup => new { backup.TenantId, backup.CreatedAt });
    }
}
