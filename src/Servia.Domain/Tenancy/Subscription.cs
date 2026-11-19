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

    public Guid? CheckoutKey { get; private set; }

    public DateTimeOffset? CheckoutDispatchedAt { get; private set; }

    public string? CheckoutUrl { get; private set; }

    public decimal? ContractPriceAmount { get; private set; }

    public string? ContractCurrency { get; private set; }

    public int? BillingIntervalMonths { get; private set; }

    public string? FirstPaymentId { get; private set; }

    public DateOnly? CheckoutFailedOn { get; private set; }

    public DateTimeOffset? PastDueSince { get; private set; }

    public DateOnly? CancellationRequestedOn { get; private set; }

    public DateOnly? CancellationEffectiveOn { get; private set; }

    /// <summary>Indica se a assinatura está em período de teste.</summary>
    public bool IsOnTrial => Status == SubscriptionStatus.Trialing;

    /// <summary>Indica se a assinatura permite operação normal.</summary>
    public bool IsActive => Status == SubscriptionStatus.Active;

    /// <summary>
    /// Inicia o trial de 14 dias pelo provisionamento do estabelecimento.
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

    /// <summary>Registra uma tentativa separada do plano efetivo.</summary>
    public static Subscription StartCheckout(Guid tenantId, Plan plan, Guid checkoutKey, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (tenantId == Guid.Empty || checkoutKey == Guid.Empty || !plan.IsActive)
        {
            throw new DomainRuleException("Estabelecimento, plano ativo e chave da contratação são obrigatórios.");
        }

        return new Subscription(Guid.NewGuid(), tenantId, plan.Id, SubscriptionStatus.PendingCheckout,
            today, today, null)
        {
            CheckoutKey = checkoutKey,
            ContractPriceAmount = plan.MonthlyPrice.Amount,
            ContractCurrency = plan.MonthlyPrice.Currency,
            BillingIntervalMonths = 1
        };
    }

    public void MarkCheckoutDispatched(DateTimeOffset now)
    {
        if (CheckoutKey is null || Status != SubscriptionStatus.PendingCheckout || CheckoutDispatchedAt.HasValue
            || GatewaySubscriptionId is not null)
        {
            throw new DomainRuleException("A tentativa já foi encaminhada para contratação.");
        }
        CheckoutDispatchedAt = now.ToUniversalTime();
    }

    public void BindCheckout(string gatewaySubscriptionId, string? checkoutUrl = null)
    {
        var id = NormalizeExternalId(gatewaySubscriptionId);
        if (Status != SubscriptionStatus.PendingCheckout || (GatewaySubscriptionId is not null && GatewaySubscriptionId != id))
        {
            throw new DomainRuleException("A tentativa não pode receber este identificador externo.");
        }
        GatewaySubscriptionId = id;
        CheckoutUrl = checkoutUrl ?? CheckoutUrl;
    }

    public void AwaitConfirmation()
    {
        if (Status != SubscriptionStatus.PendingCheckout || GatewaySubscriptionId is null)
        {
            throw new DomainRuleException("A confirmação exige um checkout pendente com assinatura externa.");
        }
        Status = SubscriptionStatus.PendingConfirmation;
    }

    public void ConfirmAuthorization(string gatewaySubscriptionId)
    {
        RequireGatewaySubscription(gatewaySubscriptionId);
        if (Status is not (SubscriptionStatus.PendingCheckout or SubscriptionStatus.PendingConfirmation))
        {
            throw new DomainRuleException("A autorização exige uma tentativa pendente.");
        }
        Status = SubscriptionStatus.AuthorizedAwaitingFirstPayment;
        CheckoutUrl = null;
    }

    /// <summary>Consome evidência já verificada pelo backend. A capacidade deve ser lida na mesma transação.</summary>
    public void ConfirmFirstPayment(string gatewaySubscriptionId, string approvedPaymentId,
        Plan plan, int eligibleProfessionals, DateOnly periodStart, DateOnly periodEnd)
    {
        RequireGatewaySubscription(gatewaySubscriptionId);
        var paymentId = NormalizeExternalId(approvedPaymentId);
        if (Status != SubscriptionStatus.AuthorizedAwaitingFirstPayment || plan.Id != PlanId
            || !plan.AllowsProfessionals(eligibleProfessionals) || periodStart < CurrentPeriodStart || periodEnd <= periodStart)
        {
            throw new DomainRuleException("O primeiro pagamento não pode efetivar esta assinatura ou capacidade.");
        }

        FirstPaymentId = paymentId;
        CurrentPeriodStart = periodStart;
        CurrentPeriodEnd = periodEnd;
        Status = SubscriptionStatus.Active;
    }

    public void FailCheckout(DateOnly today)
    {
        if (Status is not (SubscriptionStatus.PendingCheckout or SubscriptionStatus.PendingConfirmation
            or SubscriptionStatus.AuthorizedAwaitingFirstPayment) || today < CurrentPeriodStart)
        {
            throw new DomainRuleException("Somente uma tentativa pendente pode ser encerrada por falha.");
        }
        CheckoutFailedOn = today;
        CheckoutUrl = null;
        Status = SubscriptionStatus.Cancelled;
    }

    public void ExpireTrial(DateOnly today)
    {
        if (!IsTrialExpired(today))
        {
            throw new DomainRuleException("A vigência do trial ainda não terminou.");
        }
        Status = SubscriptionStatus.Expired;
    }

    public void MarkPastDue(DateTimeOffset now)
    {
        if (Status is not (SubscriptionStatus.Active or SubscriptionStatus.PastDue))
        {
            throw new DomainRuleException("Somente uma assinatura paga pode ficar inadimplente.");
        }
        PastDueSince ??= now.ToUniversalTime();
        Status = SubscriptionStatus.PastDue;
    }

    public void ConfirmRegularization(string gatewaySubscriptionId, string approvedPaymentId,
        DateOnly periodStart, DateOnly periodEnd)
    {
        RequireGatewaySubscription(gatewaySubscriptionId);
        var paymentId = NormalizeExternalId(approvedPaymentId);
        if (Status != SubscriptionStatus.PastDue || paymentId == FirstPaymentId
            || periodStart < CurrentPeriodEnd || periodEnd <= periodStart
            || CancellationEffectiveOn.HasValue)
        {
            throw new DomainRuleException("O pagamento não regulariza o período da assinatura.");
        }
        CurrentPeriodStart = periodStart;
        CurrentPeriodEnd = periodEnd;
        PastDueSince = null;
        Status = SubscriptionStatus.Active;
    }

    public void CancelTrial(DateOnly today)
    {
        if (!IsOnTrial || today < CurrentPeriodStart)
        {
            throw new DomainRuleException("Apenas um trial vigente pode ser cancelado localmente.");
        }
        CancellationRequestedOn = today;
        CancellationEffectiveOn = today;
        Status = SubscriptionStatus.Cancelled;
    }

    public void RequestCancellation(DateOnly today)
    {
        if (Status is not (SubscriptionStatus.Active or SubscriptionStatus.PastDue))
        {
            throw new DomainRuleException("Somente uma assinatura paga vigente pode solicitar cancelamento.");
        }
        CancellationRequestedOn ??= today;
    }

    public void ConfirmCancellation(string gatewaySubscriptionId)
    {
        RequireGatewaySubscription(gatewaySubscriptionId);
        if (CancellationRequestedOn is null || Status is not (SubscriptionStatus.Active or SubscriptionStatus.PastDue))
        {
            throw new DomainRuleException("O cancelamento precisa ter sido solicitado.");
        }
        CancellationEffectiveOn ??= CurrentPeriodEnd;
    }

    public void EndCancellation(DateOnly today)
    {
        if (CancellationEffectiveOn is not { } effective || today < effective
            || Status is not (SubscriptionStatus.Active or SubscriptionStatus.PastDue))
        {
            throw new DomainRuleException("O encerramento exige cancelamento confirmado e fim do período pago.");
        }
        Status = SubscriptionStatus.Cancelled;
    }

    private void RequireGatewaySubscription(string gatewaySubscriptionId)
    {
        if (GatewaySubscriptionId is null || GatewaySubscriptionId != NormalizeExternalId(gatewaySubscriptionId))
        {
            throw new DomainRuleException("A confirmação pertence a outra assinatura externa.");
        }
    }

    private static string NormalizeExternalId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Trim().Length > 100)
        {
            throw new DomainRuleException("Identificador externo inválido.");
        }
        return id.Trim();
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
    Cancelled = 4,

    PendingCheckout = 5,
    PendingConfirmation = 6,
    AuthorizedAwaitingFirstPayment = 7,
    Expired = 8
}
