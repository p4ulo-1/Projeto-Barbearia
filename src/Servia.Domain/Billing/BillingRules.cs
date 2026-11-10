using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Billing;

/// <summary>
/// Regras comerciais do billing da plataforma.
///
/// Concentra as decisões que valem para todos os tenants: módulos liberados,
/// upgrade por excedente de profissionais, limite de personalização do site
/// e franquia de IA por plano.
///
/// <b>Independência (F4.13):</b> nada aqui referencia o financeiro do
/// estabelecimento. Billing e financeiro são domínios separados (ADR-020).
/// </summary>
public static class BillingRules
{
    /// <summary>
    /// Indica se o plano libera um módulo (F4.9).
    ///
    /// Todos os planos oferecem todos os módulos: a diferenciação está na
    /// quantidade de profissionais, no grau de personalização e na franquia
    /// de IA. Franquia de consumo não é bloqueio de módulo.
    /// </summary>
    public static bool IsModuleAvailable(Plan plan, ModuleType module)
    {
        ArgumentNullException.ThrowIfNull(plan);

        // Todos os planos liberam todos os módulos.
        return plan.IsActive;
    }

    /// <summary>
    /// Indica se o tenant precisa subir de plano por excedente de
    /// profissionais (F4.10).
    ///
    /// Não existe taxa por excedente: ao ultrapassar o limite, o tenant
    /// precisa mudar de categoria.
    /// </summary>
    public static bool RequiresUpgrade(Plan plan, int professionalCount)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return !plan.AllowsProfessionals(professionalCount);
    }

    /// <summary>
    /// Valida o grau de personalização do site contra o plano (F4.11).
    /// </summary>
    public static void EnsureCustomizationAllowed(Plan plan, SiteCustomizationLevel level)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (!plan.AllowsCustomization(level))
        {
            throw new DomainRuleException(
                $"O plano {plan.Name} não permite o nível de personalização {level}.");
        }
    }

    /// <summary>
    /// Retorna a franquia mensal de IA do plano (F4.15).
    /// </summary>
    public static int AiAllowance(Plan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return plan.IncludedAiUnits;
    }

    /// <summary>
    /// Calcula o valor do consumo excedente de um recurso (F4.17).
    ///
    /// Só o que ultrapassa a franquia é cobrado. Consumo dentro da franquia
    /// não gera cobrança.
    /// </summary>
    public static Money CalculateOverage(UsageQuota quota, Money unitPrice)
    {
        ArgumentNullException.ThrowIfNull(quota);

        var overage = quota.Consumed - quota.Included;

        if (overage <= 0)
        {
            return Money.Zero(unitPrice.Currency);
        }

        return unitPrice.MultiplyBy(overage);
    }

    /// <summary>
    /// Indica se o consumo deve ser cobrado na fatura seguinte (F4.17).
    /// </summary>
    public static bool ShouldBillConsumption(UsageQuota quota)
    {
        ArgumentNullException.ThrowIfNull(quota);

        return quota.BillingMode == ConsumptionBillingMode.Automatic
               && quota.IsOverIncluded;
    }
}

/// <summary>Módulos da plataforma.</summary>
public enum ModuleType
{
    Membership = 1,
    Packages = 2,
    Commissions = 3,
    WhatsAppAutomation = 4,
    Loyalty = 5,
    AiSiteBuilder = 6,
    OnlinePayments = 7
}
