using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Servia.Domain.Appointments;
using Servia.Domain.Ai;
using Servia.Domain.Auditing;
using Servia.Domain.Billing;
using Servia.Domain.Finance;
using Servia.Domain.Loyalty;
using Servia.Domain.Memberships;
using Servia.Domain.Messaging;
using Servia.Domain.Operations;
using Servia.Domain.Sites;
using Servia.Domain.Catalog;
using Servia.Domain.Customers;
using Servia.Domain.Identity;
using Servia.Domain.Team;
using Servia.Domain.Tenancy;

namespace Servia.Infrastructure.Persistence;

/// <summary>
/// Contexto de persistência do Servia.
///
/// Aplica o <b>filtro global por tenant</b> (ADR-016): toda entidade que
/// implementa <see cref="ITenantScoped"/> tem suas consultas restritas ao
/// tenant corrente, sem que cada consulta precise lembrar de filtrar.
///
/// <b>Comportamento sem tenant:</b> quando não há tenant resolvido, o filtro
/// nega acesso a dados de negócio. Isso é intencional — falhar fechado é
/// melhor do que vazar dados. Operações administrativas da plataforma devem
/// usar um contexto explícito, nunca um tenant arbitrário.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext)
    : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Plan> Plans => Set<Plan>();

    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    public DbSet<PlanChange> PlanChanges => Set<PlanChange>();

    public DbSet<Appointment> Appointments => Set<Appointment>();

    public DbSet<Service> Services => Set<Service>();

    public DbSet<Professional> Professionals => Set<Professional>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Invitation> Invitations => Set<Invitation>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    // Catalogo
    public DbSet<ServiceCategory> ServiceCategories => Set<ServiceCategory>();

    public DbSet<ServiceVariant> ServiceVariants => Set<ServiceVariant>();

    public DbSet<ServiceCombo> ServiceCombos => Set<ServiceCombo>();

    // Equipe
    public DbSet<ProfessionalService> ProfessionalServices => Set<ProfessionalService>();

    public DbSet<ScheduleBlock> ScheduleBlocks => Set<ScheduleBlock>();

    // Clientes
    public DbSet<CustomerPreference> CustomerPreferences => Set<CustomerPreference>();

    // Agenda
    public DbSet<WaitlistEntry> WaitlistEntries => Set<WaitlistEntry>();

    public DbSet<ScheduleLock> ScheduleLocks => Set<ScheduleLock>();

    // Sites
    public DbSet<Site> Sites => Set<Site>();

    // Onboarding assistido por IA (itens 1 e 2) e arquivos do site.
    public DbSet<OnboardingSession> OnboardingSessions => Set<OnboardingSession>();

    public DbSet<AiChangeSet> AiChangeSets => Set<AiChangeSet>();

    /// <summary>Alterações direcionadas do site (C12).</summary>
    public DbSet<AiSiteChange> AiSiteChanges => Set<AiSiteChange>();

    public DbSet<SiteAsset> SiteAssets => Set<SiteAsset>();

    /// <summary>Contadores de tentativas de agendamento público (C3).</summary>
    public DbSet<PublicBookingAttempt> PublicBookingAttempts => Set<PublicBookingAttempt>();

    // Billing
    public DbSet<PlatformInvoice> PlatformInvoices => Set<PlatformInvoice>();

    public DbSet<UsageQuota> UsageQuotas => Set<UsageQuota>();

    public DbSet<CreditPurchase> CreditPurchases => Set<CreditPurchase>();

    public DbSet<SiteChangeCharge> SiteChangeCharges => Set<SiteChangeCharge>();

    // Financeiro
    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<Transaction> Transactions => Set<Transaction>();

    public DbSet<CommissionRule> CommissionRules => Set<CommissionRule>();

    public DbSet<CommissionEntry> CommissionEntries => Set<CommissionEntry>();

    // Mensageria
    public DbSet<MessageTemplate> MessageTemplates => Set<MessageTemplate>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<MessagingPolicy> MessagingPolicies => Set<MessagingPolicy>();

    // Modulos opcionais
    public DbSet<MembershipPlan> MembershipPlans => Set<MembershipPlan>();

    public DbSet<Membership> Memberships => Set<Membership>();

    public DbSet<ServicePackage> ServicePackages => Set<ServicePackage>();

    public DbSet<PackageUsage> PackageUsages => Set<PackageUsage>();

    public DbSet<LoyaltyAccount> LoyaltyAccounts => Set<LoyaltyAccount>();

    public DbSet<ReactivationCampaign> ReactivationCampaigns => Set<ReactivationCampaign>();

    public DbSet<ServiceReview> ServiceReviews => Set<ServiceReview>();

    // IA
    public DbSet<AiOperationLog> AiOperationLogs => Set<AiOperationLog>();

    // Operacoes
    public DbSet<DataSubjectRequest> DataSubjectRequests => Set<DataSubjectRequest>();

    public DbSet<TenantBackup> TenantBackups => Set<TenantBackup>();

    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        ApplyTenantFilter(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Aplica o filtro global a todas as entidades que implementam
    /// <see cref="ITenantScoped"/>, sem precisar listá-las uma a uma.
    /// </summary>
    private void ApplyTenantFilter(ModelBuilder modelBuilder)
    {
        var tenantScopedTypes = modelBuilder.Model
            .GetEntityTypes()
            .Where(entityType => typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType));

        foreach (var entityType in tenantScopedTypes)
        {
            var parameter = Expression.Parameter(entityType.ClrType, "entity");
            var tenantProperty = Expression.Property(parameter, nameof(ITenantScoped.TenantId));

            // entity.TenantId == tenantContext.TenantId
            //
            // CurrentTenantId e Guid?: quando nao ha tenant, a comparacao
            // com null e falsa e nada e retornado. O TenantId da entidade
            // e convertido para Guid? para que a comparacao seja valida.
            var contextProperty = Expression.Property(
                Expression.Constant(this),
                nameof(CurrentTenantId));

            var liftedTenant = Expression.Convert(tenantProperty, typeof(Guid?));

            var body = Expression.Equal(liftedTenant, contextProperty);

            var filter = Expression.Lambda(body, parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
        }
    }

    /// <summary>
    /// Tenant corrente, exposto para o filtro global (Etapa 7, R1).
    ///
    /// Retorna <c>null</c> quando nao ha tenant resolvido. O filtro compara
    /// com <c>null</c>, que nunca e igual a um <c>TenantId</c> real — o
    /// acesso e negado sem depender de uma sentinela.
    ///
    /// <b>Por que nao <c>Guid.Empty</c>:</b> o administrador da plataforma
    /// tem <c>TenantId = Guid.Empty</c>. Usar o mesmo valor como sentinela
    /// faria os dados dele vazarem para qualquer requisicao sem tenant.
    /// </summary>
    public Guid? CurrentTenantId => tenantContext.TenantId;
}
