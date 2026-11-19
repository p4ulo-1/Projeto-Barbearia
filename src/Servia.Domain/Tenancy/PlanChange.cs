using Servia.Domain.Common;

namespace Servia.Domain.Tenancy;

/// <summary>
/// Registro imutável de uma mudança de plano do tenant (F16.5).
///
/// Cada contratação, upgrade, downgrade ou upgrade forçado gera um
/// registro. O histórico permite responder "quando este estabelecimento
/// mudou de categoria e por quê" — pergunta que aparece em disputa
/// comercial e em auditoria.
///
/// <b>Imutabilidade:</b> não há método de alteração. Um registro errado
/// não é corrigido: é seguido por outro que o substitui.
/// </summary>
public sealed class PlanChange : ITenantScoped
{
    private PlanChange(
        Guid id,
        Guid tenantId,
        Guid subscriptionId,
        Guid? fromPlanId,
        Guid toPlanId,
        PlanChangeReason reason,
        Guid? changedBy,
        DateTimeOffset changedAt)
    {
        Id = id;
        TenantId = tenantId;
        SubscriptionId = subscriptionId;
        FromPlanId = fromPlanId;
        ToPlanId = toPlanId;
        Reason = reason;
        ChangedBy = changedBy;
        ChangedAt = changedAt;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Record"/>.
    /// </summary>
    private PlanChange()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid SubscriptionId { get; private set; }

    /// <summary>Plano anterior. Nulo na contratação inicial.</summary>
    public Guid? FromPlanId { get; private set; }

    /// <summary>Plano escolhido.</summary>
    public Guid ToPlanId { get; private set; }

    public PlanChangeReason Reason { get; private set; }

    /// <summary>Usuário que efetivou a mudança, quando há uma ação autenticada.</summary>
    public Guid? ChangedBy { get; private set; }

    public DateTimeOffset ChangedAt { get; private set; }

    public PlanChangeStatus Status { get; private set; } = PlanChangeStatus.Effective;

    public DateOnly? EffectiveOn { get; private set; }
    public DateTimeOffset? EffectiveAtUtc { get; private set; }
    public DateTimeOffset? AppliedAtUtc { get; private set; }
    public decimal? RequestedPrice { get; private set; }
    public string? RequestedCurrency { get; private set; }
    public Guid? OperationKey { get; private set; }
    public decimal? SourcePrice { get; private set; }
    public decimal? AdditionalAmount { get; private set; }
    public DateTimeOffset? CycleStart { get; private set; }
    public DateTimeOffset? CycleEnd { get; private set; }
    public DateTimeOffset? PaymentDispatchedAt { get; private set; }
    public string? AdditionalPaymentId { get; private set; }
    public DateTimeOffset? PaymentApprovedAt { get; private set; }
    public DateTimeOffset? RecurrenceDispatchedAt { get; private set; }
    public DateTimeOffset? RecurrenceConfirmedAt { get; private set; }
    public string? Issue { get; private set; }

    /// <summary>Registra uma solicitação sem trocar o plano vigente.</summary>
    public static PlanChange Request(Subscription subscription, Plan current, Plan target,
        int eligibleProfessionals, Guid requestedBy, DateTimeOffset now, Guid? operationKey = null,
        DateTimeOffset? cycleStart = null)
    {
        var trial = subscription.IsOnTrial;
        if ((!subscription.IsActive && !trial) || current.Id != subscription.EffectivePlanId || !target.IsActive
            || !target.AllowsProfessionals(eligibleProfessionals))
        {
            throw new DomainRuleException("A assinatura ou a capacidade do plano de destino não permite a mudança.");
        }

        var isDowngrade = target.MonthlyPrice.Amount < current.MonthlyPrice.Amount;
        if (trial && isDowngrade)
            throw new DomainRuleException("O downgrade está disponível para ciclos pagos.");
        var change = Record(subscription.TenantId, subscription.Id, current.Id, target.Id,
            isDowngrade ? PlanChangeReason.Downgrade : PlanChangeReason.Upgrade, requestedBy, now);
        change.Status = PlanChangeStatus.Requested;
        change.EffectiveOn = isDowngrade ? subscription.CurrentPeriodEnd : null;
        change.EffectiveAtUtc = isDowngrade ? subscription.ChargeDeadline : null;
        change.RequestedPrice = target.MonthlyPrice.Amount;
        change.RequestedCurrency = target.MonthlyPrice.Currency;
        change.OperationKey = operationKey;
        change.SourcePrice = subscription.ContractPriceAmount;
        change.CycleStart = cycleStart;
        change.CycleEnd = trial ? subscription.TrialDeadline : subscription.ChargeDeadline;
        if (!trial && !isDowngrade && operationKey.HasValue)
        {
            if (!cycleStart.HasValue || !change.CycleEnd.HasValue ||
                subscription.ContractPriceAmount is not { } fromPrice || subscription.ContractCurrency != target.MonthlyPrice.Currency)
                throw new DomainRuleException("O período pago e a moeda precisam ser confirmados.");
            change.AdditionalAmount = Prorate(fromPrice, target.MonthlyPrice.Amount, cycleStart.Value, change.CycleEnd.Value, now);
        }
        return change;
    }

    public static decimal Prorate(decimal current, decimal target, DateTimeOffset start, DateTimeOffset end, DateTimeOffset now)
    {
        if (now < start || now >= end || target <= current)
            throw new DomainRuleException("O ciclo pago ou a diferença de preço não permite upgrade.");
        return decimal.Round((target - current) * (end - now).Ticks / (end - start).Ticks, 2, MidpointRounding.AwayFromZero);
    }

    public bool HasExternalEffects => PaymentDispatchedAt.HasValue || RecurrenceDispatchedAt.HasValue;
    public void DispatchPayment(DateTimeOffset at) { PaymentDispatchedAt = at; Issue = null; }
    public void ApprovePayment(string id, DateTimeOffset at) { AdditionalPaymentId = id; PaymentApprovedAt = at; Issue = null; }
    public void DispatchRecurrence(DateTimeOffset at) { RecurrenceDispatchedAt = at; Issue = null; }
    public void ConfirmRecurrence(DateTimeOffset at) { RecurrenceConfirmedAt ??= at; Issue = null; }
    public void Flag(string issue) => Issue = issue;
    public void Fail(string issue) { Issue = issue; Status = PlanChangeStatus.Failed; }
    public void ConfirmUpgrade(DateTimeOffset at)
    {
        ConfirmRecurrence(at);
        EffectiveAtUtc = at;
        AppliedAtUtc = at;
        Status = PlanChangeStatus.Effective;
    }

    public void ConfirmScheduledDowngrade(DateTimeOffset dueAt, DateTimeOffset observedAt)
    {
        if (Status != PlanChangeStatus.Requested || Reason != PlanChangeReason.Downgrade || EffectiveAtUtc != dueAt)
        {
            throw new DomainRuleException("A cobrança não corresponde ao ciclo do downgrade.");
        }
        ConfirmRecurrence(observedAt);
        AppliedAtUtc = observedAt.ToUniversalTime();
        Status = PlanChangeStatus.Effective;
    }

    /// <summary>
    /// Registra uma mudança de plano.
    /// </summary>
    public static PlanChange Record(
        Guid tenantId,
        Guid subscriptionId,
        Guid? fromPlanId,
        Guid toPlanId,
        PlanChangeReason reason,
        Guid? changedBy,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (subscriptionId == Guid.Empty)
        {
            throw new DomainRuleException("A assinatura é obrigatória.");
        }

        if (toPlanId == Guid.Empty)
        {
            throw new DomainRuleException("O plano de destino é obrigatório.");
        }

        if (changedBy == Guid.Empty || (changedBy is null && reason != PlanChangeReason.Initial))
        {
            throw new DomainRuleException("O responsável pela mudança é obrigatório.");
        }

        // Contratacao inicial nao tem plano anterior; qualquer outra
        // mudanca precisa ter, senao o historico fica incompleto.
        if (reason == PlanChangeReason.Initial && fromPlanId is not null)
        {
            throw new DomainRuleException(
                "A contratação inicial não pode ter plano anterior.");
        }

        if (reason != PlanChangeReason.Initial && fromPlanId is null)
        {
            throw new DomainRuleException(
                "Uma mudança de plano precisa registrar o plano anterior.");
        }

        if (fromPlanId == toPlanId)
        {
            throw new DomainRuleException("O plano de destino é igual ao atual.");
        }

        return new PlanChange(
            Guid.NewGuid(), tenantId, subscriptionId, fromPlanId, toPlanId,
            reason, changedBy, now);
    }
}

public enum PlanChangeStatus
{
    Effective = 0,
    Requested = 1,
    Failed = 2
}

/// <summary>Motivo da mudança de plano.</summary>
public enum PlanChangeReason
{
    /// <summary>Contratação inicial, sem plano anterior.</summary>
    Initial = 1,

    /// <summary>Subiu de categoria por escolha do cliente.</summary>
    Upgrade = 2,

    /// <summary>Desceu de categoria por escolha do cliente.</summary>
    Downgrade = 3,

    /// <summary>
    /// Subiu de categoria porque ultrapassou a faixa de profissionais do
    /// plano atual. Não é escolha: é consequência da regra comercial.
    /// </summary>
    ForcedUpgrade = 4
}
