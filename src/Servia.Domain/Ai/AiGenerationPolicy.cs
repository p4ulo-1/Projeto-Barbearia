using Servia.Domain.Billing;
using Servia.Domain.Common;
using Servia.Domain.Sites;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Ai;

/// <summary>
/// Regras de uso da IA de site (F8.7–F8.12).
///
/// Concentra as decisões que controlam a geração:
///
///   - <b>Personalização por plano</b> (F8.7): o plano limita o grau de
///     personalização disponível.
///   - <b>Cobrança pós-lançamento</b> (F8.8): alterações após a publicação
///     geram cobrança; a geração inicial está incluída.
///   - <b>Franquia de IA</b> (F8.9): vem do plano, com excedente cobrado.
///   - <b>Fallback</b> (F8.12): quando a IA falha, o site usa um modelo
///     padrão em vez de ficar sem site.
/// </summary>
public static class AiGenerationPolicy
{
    /// <summary>
    /// Verifica se o plano permite o nível de personalização solicitado
    /// (F8.7).
    /// </summary>
    public static void EnsureCustomizationAllowed(Plan plan, SiteCustomizationLevel level)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (!plan.AllowsCustomization(level))
        {
            throw new DomainRuleException(
                $"O plano {plan.Name} não permite o nível de personalização {level}. "
                + "Faça upgrade para usar este recurso.");
        }
    }

    /// <summary>
    /// Indica se a operação gera cobrança (F8.8).
    ///
    /// A geração inicial e a publicação estão incluídas no plano. Apenas
    /// alterações posteriores ao lançamento são cobradas.
    /// </summary>
    public static bool IsChargeable(AiOperationType operationType) =>
        operationType == AiOperationType.PostLaunchChange;

    /// <summary>
    /// Verifica se há franquia disponível para a operação (F8.9).
    /// Lança quando a cota está esgotada.
    /// </summary>
    public static void EnsureQuotaAvailable(UsageQuota quota, int unitsRequired)
    {
        ArgumentNullException.ThrowIfNull(quota);

        if (quota.Resource != QuotaResource.AiUnits)
        {
            throw new DomainRuleException("A cota informada não é de unidades de IA.");
        }

        if (unitsRequired <= 0)
        {
            throw new DomainRuleException("O consumo estimado deve ser maior que zero.");
        }

        if (quota.Remaining < unitsRequired)
        {
            throw new DomainRuleException(
                $"Franquia de IA insuficiente: restam {quota.Remaining} unidades. "
                + "Contrate créditos adicionais para continuar.");
        }
    }

    /// <summary>
    /// Consome as unidades da operação, após verificar a franquia.
    /// Retorna falso quando não há saldo suficiente.
    /// </summary>
    public static bool TryConsume(UsageQuota quota, int units)
    {
        ArgumentNullException.ThrowIfNull(quota);

        if (units <= 0)
        {
            return false;
        }

        return quota.TryConsume(units);
    }

    /// <summary>
    /// Indica se a operação deve usar o modelo padrão de fallback (F8.12).
    /// </summary>
    public static bool ShouldUseFallback(AiGenerationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return !result.Success || result.ChangeSet is null;
    }
}
