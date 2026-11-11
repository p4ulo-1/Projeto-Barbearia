using Servia.Domain.Common;

namespace Servia.Domain.Tenancy;

/// <summary>
/// Assinatura de um tenant a um plano.
///
/// Protege o ciclo de vida comercial: trial de 14 dias, renovação por
/// período, marcação de inadimplência e cancelamento ao fim do período.
///
/// A assinatura pertence ao <b>billing da plataforma</b>, não ao financeiro
/// do estabelecimento (ADR-020).
///
/// <b>Isolamento (Etapa 2 da auditoria):</b> implementa
/// <see cref="ITenantScoped"/> para que o filtro global do EF Core restrinja
/// a consulta ao tenant corrente. Sem isso, um estabelecimento podia ler a
/// assinatura de outro.
/// </summary>
public sealed class Subscription : ITenantScoped
{
    public const int TrialDays = 14;

    private Subscription(
        Guid id,
        Guid tenantId,
        Guid planId,
        SubscriptionStatus status,
        DateOnly currentPeriodStart,
        DateOnly currentPeriodEnd,
        DateOnly? trialEndsAt)
    {
        Id = id;
        TenantId = tenantId;
        PlanId = planId;
        Status = status;
        CurrentPeriodStart = currentPeriodStart;
        CurrentPeriodEnd = currentPeriodEnd;
        TrialEndsAt = trialEndsAt;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="StartTrial"/>.
    /// </summary>
    private Subscription()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid PlanId { get; private set; }

    public SubscriptionStatus Status { get; private set; }

    public DateOnly CurrentPeriodStart { get; private set; }

    public DateOnly CurrentPeriodEnd { get; private set; }

    public DateOnly? TrialEndsAt { get; private set; }

    /// <summary>Identificador do cliente no gateway de pagamento.</summary>
    public string? GatewayCustomerId { get; private set; }

    /// <summary>Identificador da assinatura no gateway de pagamento.</summary>
    public string? GatewaySubscriptionId { get; private set; }

    /// <summary>Indica se a assinatura está em período de teste.</summary>
    public bool IsOnTrial => Status == SubscriptionStatus.Trialing;

    /// <summary>Indica se a assinatura permite operação normal.</summary>
    public bool IsActive => Status == SubscriptionStatus.Active;

    /// <summary>
    /// Inicia o trial de 14 dias com cartão cadastrado.
    /// </summary>
    public static Subscription StartTrial(
        Guid tenantId,
        Guid planId,
        DateOnly today,
        string? gatewayCustomerId = null)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (planId == Guid.Empty)
        {
            throw new DomainRuleException("O plano é obrigatório.");
        }

        var trialEnd = today.AddDays(TrialDays);

        return new Subscription(
            Guid.NewGuid(),
            tenantId,
            planId,
            SubscriptionStatus.Trialing,
            today,
            trialEnd,
            trialEnd)
        {
            GatewayCustomerId = gatewayCustomerId
        };
    }

    /// <summary>
    /// Converte o trial em assinatura ativa após o débito do cartão.
    /// </summary>
    public void ActivateAfterTrial(DateOnly today, string? gatewaySubscriptionId = null)
    {
        if (Status != SubscriptionStatus.Trialing)
        {
            throw new DomainRuleException("Somente assinaturas em trial podem ser ativadas.");
        }

        Status = SubscriptionStatus.Active;
        CurrentPeriodStart = today;
        CurrentPeriodEnd = today.AddMonths(1);
        TrialEndsAt = null;
        GatewaySubscriptionId = gatewaySubscriptionId;
    }

    /// <summary>Renova a assinatura por mais um período mensal.</summary>
    public void Renew(DateOnly today)
    {
        if (Status is SubscriptionStatus.Cancelled)
        {
            throw new DomainRuleException("Uma assinatura cancelada não pode ser renovada.");
        }

        Status = SubscriptionStatus.Active;
        CurrentPeriodStart = today;
        CurrentPeriodEnd = today.AddMonths(1);
    }

    /// <summary>Marca a assinatura como inadimplente.</summary>
    public void MarkPastDue()
    {
        if (Status is SubscriptionStatus.Cancelled)
        {
            throw new DomainRuleException("Uma assinatura cancelada não pode ficar inadimplente.");
        }

        Status = SubscriptionStatus.PastDue;
    }

    /// <summary>
    /// Cancela a assinatura. O acesso permanece até o fim do período pago.
    /// </summary>
    public void Cancel()
    {
        if (Status is SubscriptionStatus.Cancelled)
        {
            throw new DomainRuleException("A assinatura já está cancelada.");
        }

        Status = SubscriptionStatus.Cancelled;
    }

    /// <summary>Indica se o trial expirou na data informada.</summary>
    public bool IsTrialExpired(DateOnly today) =>
        IsOnTrial && TrialEndsAt.HasValue && today >= TrialEndsAt.Value;

    /// <summary>Indica se o período pago terminou na data informada.</summary>
    public bool IsPeriodExpired(DateOnly today) => today > CurrentPeriodEnd;
}

/// <summary>Estado da assinatura do tenant.</summary>
public enum SubscriptionStatus
{
    /// <summary>Em período de teste.</summary>
    Trialing = 1,

    /// <summary>Ativa e em dia.</summary>
    Active = 2,

    /// <summary>Inadimplente.</summary>
    PastDue = 3,

    /// <summary>Cancelada. Estado terminal.</summary>
    Cancelled = 4
}
