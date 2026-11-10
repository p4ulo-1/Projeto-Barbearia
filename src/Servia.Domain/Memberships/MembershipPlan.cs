using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Memberships;

/// <summary>
/// Clube de assinatura oferecido pelo estabelecimento (F7.1).
///
/// O cliente paga uma mensalidade e recebe benefícios definidos: quantidade
/// de serviços por período ou desconto percentual.
///
/// O plano é o molde; a assinatura de cada cliente é uma <see cref="Membership"/>.
/// </summary>
public sealed class MembershipPlan : ITenantScoped
{
    private readonly List<MembershipBenefit> _benefits = [];

    private MembershipPlan(
        Guid id,
        Guid tenantId,
        string name,
        Money monthlyPrice,
        bool isActive)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        MonthlyPrice = monthlyPrice;
        IsActive = isActive;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>.
    /// </summary>
    private MembershipPlan()
    {
        Name = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; }

    public Money MonthlyPrice { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyCollection<MembershipBenefit> Benefits => _benefits;

    public static MembershipPlan Create(
        Guid tenantId,
        string name,
        Money monthlyPrice)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainRuleException("O nome do clube é obrigatório.");
        }

        if (monthlyPrice.Amount <= 0)
        {
            throw new DomainRuleException("A mensalidade deve ser maior que zero.");
        }

        return new MembershipPlan(Guid.NewGuid(), tenantId, name.Trim(), monthlyPrice, isActive: true);
    }

    /// <summary>
    /// Adiciona um benefício de quantidade: N usos de um serviço por período.
    /// </summary>
    public MembershipBenefit AddServiceBenefit(Guid serviceId, int quantityPerPeriod)
    {
        if (serviceId == Guid.Empty)
        {
            throw new DomainRuleException("O serviço é obrigatório.");
        }

        if (quantityPerPeriod <= 0)
        {
            throw new DomainRuleException("A quantidade por período deve ser maior que zero.");
        }

        var benefit = MembershipBenefit.ForService(serviceId, quantityPerPeriod);
        _benefits.Add(benefit);

        return benefit;
    }

    /// <summary>
    /// Adiciona um benefício de desconto percentual sobre qualquer serviço.
    /// </summary>
    public MembershipBenefit AddDiscountBenefit(decimal discountPercentage)
    {
        if (discountPercentage is <= 0 or > 100)
        {
            throw new DomainRuleException("O desconto deve estar entre 0 e 100.");
        }

        var benefit = MembershipBenefit.ForDiscount(discountPercentage);
        _benefits.Add(benefit);

        return benefit;
    }

    /// <summary>Altera a mensalidade do clube.</summary>
    public void ChangePrice(Money monthlyPrice)
    {
        if (monthlyPrice.Amount <= 0)
        {
            throw new DomainRuleException("A mensalidade deve ser maior que zero.");
        }

        MonthlyPrice = monthlyPrice;
    }

    /// <summary>Desativa o clube, impedindo novas assinaturas.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Reativa o clube.</summary>
    public void Activate() => IsActive = true;
}

/// <summary>
/// Benefício de um clube: quantidade de um serviço ou desconto percentual.
/// </summary>
public sealed class MembershipBenefit
{
    private MembershipBenefit(
        Guid? serviceId,
        int quantityPerPeriod,
        decimal? discountPercentage)
    {
        ServiceId = serviceId;
        QuantityPerPeriod = quantityPerPeriod;
        DiscountPercentage = discountPercentage;
    }

    /// <summary>Serviço do benefício. Nulo para benefício de desconto.</summary>
    public Guid? ServiceId { get; private set; }

    /// <summary>Quantidade de usos por período. Zero para benefício de desconto.</summary>
    public int QuantityPerPeriod { get; private set; }

    /// <summary>Desconto percentual. Nulo para benefício de quantidade.</summary>
    public decimal? DiscountPercentage { get; private set; }

    /// <summary>Indica se o benefício é de desconto percentual.</summary>
    public bool IsDiscount => DiscountPercentage.HasValue;

    internal static MembershipBenefit ForService(Guid serviceId, int quantity) =>
        new(serviceId, quantity, null);

    internal static MembershipBenefit ForDiscount(decimal percentage) =>
        new(null, 0, percentage);
}
