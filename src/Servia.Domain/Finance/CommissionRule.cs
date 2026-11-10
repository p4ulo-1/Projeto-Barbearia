using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Finance;

/// <summary>
/// Regra de comissão de um profissional (F5.6).
///
/// A regra pode ser percentual, valor fixo ou por meta. A especificidade
/// define a precedência: uma regra por serviço vence uma regra geral do
/// profissional, que vence a regra padrão do estabelecimento.
///
/// Isso evita condicionais no cálculo: a precedência é uma propriedade da
/// regra, não uma decisão do chamador.
/// </summary>
public sealed class CommissionRule : ITenantScoped
{
    private CommissionRule(
        Guid id,
        Guid tenantId,
        Guid? professionalId,
        Guid? serviceId,
        CommissionType type,
        decimal percentage,
        Money? fixedAmount,
        Money? targetAmount)
    {
        Id = id;
        TenantId = tenantId;
        ProfessionalId = professionalId;
        ServiceId = serviceId;
        Type = type;
        Percentage = percentage;
        FixedAmount = fixedAmount;
        TargetAmount = targetAmount;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>.
    /// </summary>
    private CommissionRule()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>Profissional da regra. Nulo para a regra padrão do tenant.</summary>
    public Guid? ProfessionalId { get; private set; }

    /// <summary>Serviço da regra. Nulo para regra geral do profissional.</summary>
    public Guid? ServiceId { get; private set; }

    public CommissionType Type { get; private set; }

    /// <summary>Percentual sobre o valor do serviço.</summary>
    public decimal Percentage { get; private set; }

    /// <summary>Valor fixo por atendimento.</summary>
    public Money? FixedAmount { get; private set; }

    /// <summary>Meta de faturamento para a comissão por meta.</summary>
    public Money? TargetAmount { get; private set; }

    /// <summary>
    /// Especificidade da regra: quanto maior, mais prioritária.
    /// Serviço (2) vence profissional (1), que vence padrão (0).
    /// </summary>
    public int Specificity => ServiceId.HasValue ? 2 : ProfessionalId.HasValue ? 1 : 0;

    public static CommissionRule CreatePercentage(
        Guid tenantId,
        decimal percentage,
        Guid? professionalId = null,
        Guid? serviceId = null)
    {
        if (percentage is < 0 or > 100)
        {
            throw new DomainRuleException("O percentual deve estar entre 0 e 100.");
        }

        return Create(tenantId, professionalId, serviceId, CommissionType.Percentage,
            percentage, null, null);
    }

    public static CommissionRule CreateFixed(
        Guid tenantId,
        Money fixedAmount,
        Guid? professionalId = null,
        Guid? serviceId = null)
    {
        if (fixedAmount.Amount <= 0)
        {
            throw new DomainRuleException("O valor fixo deve ser maior que zero.");
        }

        return Create(tenantId, professionalId, serviceId, CommissionType.Fixed,
            0m, fixedAmount, null);
    }

    public static CommissionRule CreateTarget(
        Guid tenantId,
        decimal percentage,
        Money targetAmount,
        Guid? professionalId = null)
    {
        if (percentage is < 0 or > 100)
        {
            throw new DomainRuleException("O percentual deve estar entre 0 e 100.");
        }

        if (targetAmount.Amount <= 0)
        {
            throw new DomainRuleException("A meta deve ser maior que zero.");
        }

        return Create(tenantId, professionalId, null, CommissionType.Target,
            percentage, null, targetAmount);
    }

    private static CommissionRule Create(
        Guid tenantId,
        Guid? professionalId,
        Guid? serviceId,
        CommissionType type,
        decimal percentage,
        Money? fixedAmount,
        Money? targetAmount)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        return new CommissionRule(
            Guid.NewGuid(), tenantId, professionalId, serviceId, type,
            percentage, fixedAmount, targetAmount);
    }

    /// <summary>
    /// Calcula a comissão sobre o valor de um atendimento.
    /// </summary>
    public Money CalculateFor(Money serviceAmount)
    {
        // Money e um tipo por valor: nunca e nulo. Verificar nulidade aqui
        // seria codigo morto (CA2264).
        return Type switch
        {
            CommissionType.Percentage => serviceAmount.MultiplyBy(Percentage / 100m),
            CommissionType.Fixed => FixedAmount ?? Money.Zero(serviceAmount.Currency),
            CommissionType.Target => CalculateTarget(serviceAmount),
            _ => Money.Zero(serviceAmount.Currency)
        };
    }

    private Money CalculateTarget(Money serviceAmount)
    {
        // A comissão por meta só é devida quando o faturamento atinge a meta.
        // O cálculo do atingimento é responsabilidade do serviço de apuração,
        // que conhece o acumulado do período.
        return serviceAmount.MultiplyBy(Percentage / 100m);
    }
}

/// <summary>Tipo de cálculo de comissão.</summary>
public enum CommissionType
{
    /// <summary>Percentual sobre o valor do serviço.</summary>
    Percentage = 1,

    /// <summary>Valor fixo por atendimento.</summary>
    Fixed = 2,

    /// <summary>Percentual condicionado ao atingimento de meta.</summary>
    Target = 3
}
