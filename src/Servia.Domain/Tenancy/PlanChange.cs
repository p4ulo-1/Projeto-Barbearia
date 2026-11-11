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
        Guid changedBy,
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

    /// <summary>Usuário que solicitou a mudança.</summary>
    public Guid ChangedBy { get; private set; }

    public DateTimeOffset ChangedAt { get; private set; }

    /// <summary>
    /// Registra uma mudança de plano.
    /// </summary>
    public static PlanChange Record(
        Guid tenantId,
        Guid subscriptionId,
        Guid? fromPlanId,
        Guid toPlanId,
        PlanChangeReason reason,
        Guid changedBy,
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

        if (changedBy == Guid.Empty)
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
