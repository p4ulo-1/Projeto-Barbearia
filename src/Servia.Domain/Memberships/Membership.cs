using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Memberships;

/// <summary>
/// Assinatura de um cliente a um clube (F7.1, F7.3, F7.4).
///
/// Controla o consumo dos benefícios dentro do período e o estado da
/// assinatura. O consumo é reiniciado a cada renovação.
///
/// <b>Inadimplência (F7.4):</b> uma assinatura suspensa por falta de
/// pagamento não permite consumir benefícios, mas mantém o histórico.
/// </summary>
public sealed class Membership : ITenantScoped
{
    private readonly Dictionary<Guid, int> _consumedByService = [];

    private Membership(
        Guid id,
        Guid tenantId,
        Guid customerId,
        Guid membershipPlanId,
        DateOnly periodStart,
        DateOnly periodEnd)
    {
        Id = id;
        TenantId = tenantId;
        CustomerId = customerId;
        MembershipPlanId = membershipPlanId;
        CurrentPeriodStart = periodStart;
        CurrentPeriodEnd = periodEnd;
        Status = MembershipStatus.Active;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Subscribe"/>.
    /// </summary>
    private Membership()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid MembershipPlanId { get; private set; }

    public MembershipStatus Status { get; private set; }

    public DateOnly CurrentPeriodStart { get; private set; }

    public DateOnly CurrentPeriodEnd { get; private set; }

    /// <summary>Indica se a assinatura permite consumo.</summary>
    public bool IsActive => Status == MembershipStatus.Active;

    /// <summary>Indica se a assinatura está suspensa por inadimplência.</summary>
    public bool IsSuspended => Status == MembershipStatus.Suspended;

    /// <summary>Consumo por serviço no período corrente.</summary>
    public IReadOnlyDictionary<Guid, int> ConsumedByService => _consumedByService;

    public static Membership Subscribe(
        Guid tenantId,
        Guid customerId,
        Guid membershipPlanId,
        DateOnly periodStart,
        DateOnly periodEnd)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (customerId == Guid.Empty)
        {
            throw new DomainRuleException("O cliente é obrigatório.");
        }

        if (membershipPlanId == Guid.Empty)
        {
            throw new DomainRuleException("O clube é obrigatório.");
        }

        if (periodStart >= periodEnd)
        {
            throw new DomainRuleException("O início do período deve ser anterior ao fim.");
        }

        return new Membership(
            Guid.NewGuid(), tenantId, customerId, membershipPlanId, periodStart, periodEnd);
    }

    /// <summary>
    /// Consome um benefício de serviço (F7.3).
    /// Falha se a assinatura não estiver ativa ou se o saldo acabou.
    /// </summary>
    public void ConsumeBenefit(Guid serviceId, int quantity, MembershipPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (!IsActive)
        {
            throw new DomainRuleException(
                "A assinatura não está ativa. Regularize o pagamento para consumir benefícios.");
        }

        if (serviceId == Guid.Empty)
        {
            throw new DomainRuleException("O serviço é obrigatório.");
        }

        if (quantity <= 0)
        {
            throw new DomainRuleException("A quantidade deve ser maior que zero.");
        }

        var benefit = plan.Benefits.FirstOrDefault(item => item.ServiceId == serviceId);

        if (benefit is null)
        {
            throw new DomainRuleException("O serviço não faz parte dos benefícios deste clube.");
        }

        var consumed = _consumedByService.GetValueOrDefault(serviceId);

        if (consumed + quantity > benefit.QuantityPerPeriod)
        {
            throw new DomainRuleException(
                $"Saldo insuficiente: restam {benefit.QuantityPerPeriod - consumed} usos neste período.");
        }

        _consumedByService[serviceId] = consumed + quantity;
    }

    /// <summary>Retorna o saldo restante de um serviço no período.</summary>
    public int RemainingFor(Guid serviceId, MembershipPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var benefit = plan.Benefits.FirstOrDefault(item => item.ServiceId == serviceId);

        if (benefit is null)
        {
            return 0;
        }

        return Math.Max(0, benefit.QuantityPerPeriod - _consumedByService.GetValueOrDefault(serviceId));
    }

    /// <summary>
    /// Renova a assinatura para um novo período, zerando o consumo.
    /// </summary>
    public void Renew(DateOnly periodStart, DateOnly periodEnd)
    {
        if (periodStart >= periodEnd)
        {
            throw new DomainRuleException("O início do período deve ser anterior ao fim.");
        }

        if (Status == MembershipStatus.Cancelled)
        {
            throw new DomainRuleException("Uma assinatura cancelada não pode ser renovada.");
        }

        CurrentPeriodStart = periodStart;
        CurrentPeriodEnd = periodEnd;
        Status = MembershipStatus.Active;
        _consumedByService.Clear();
    }

    /// <summary>
    /// Suspende a assinatura por inadimplência (F7.4).
    /// O histórico de consumo é preservado.
    /// </summary>
    public void Suspend()
    {
        if (Status == MembershipStatus.Cancelled)
        {
            throw new DomainRuleException("Uma assinatura cancelada não pode ser suspensa.");
        }

        Status = MembershipStatus.Suspended;
    }

    /// <summary>Reativa a assinatura após regularização.</summary>
    public void Reactivate()
    {
        if (Status == MembershipStatus.Cancelled)
        {
            throw new DomainRuleException("Uma assinatura cancelada não pode ser reativada.");
        }

        Status = MembershipStatus.Active;
    }

    /// <summary>Cancela a assinatura. Estado terminal.</summary>
    public void Cancel()
    {
        if (Status == MembershipStatus.Cancelled)
        {
            throw new DomainRuleException("A assinatura já está cancelada.");
        }

        Status = MembershipStatus.Cancelled;
    }

    /// <summary>Indica se o período corrente expirou na data informada.</summary>
    public bool IsPeriodExpired(DateOnly today) => today > CurrentPeriodEnd;
}

/// <summary>Estado da assinatura do cliente.</summary>
public enum MembershipStatus
{
    Active = 1,
    Suspended = 2,
    Cancelled = 3
}
