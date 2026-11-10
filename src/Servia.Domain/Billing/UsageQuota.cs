using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Billing;

/// <summary>
/// Cota de consumo de um recurso medido, por tenant e período (F4.12).
///
/// Cada recurso tem sua própria cota: WhatsApp e IA são contabilizados
/// separadamente (F4.14). O consumo nunca ultrapassa o limite sem
/// autorização — o bloqueio acontece no servidor.
///
/// <b>Franquia de IA (F4.15):</b> vem do plano contratado.
/// <b>Créditos antecipados (F4.16):</b> somam-se à franquia após o pagamento.
/// </summary>
public sealed class UsageQuota : ITenantScoped
{
    private UsageQuota(
        Guid id,
        Guid tenantId,
        QuotaResource resource,
        DateOnly periodStart,
        DateOnly periodEnd,
        int included,
        ConsumptionBillingMode billingMode)
    {
        Id = id;
        TenantId = tenantId;
        Resource = resource;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        Included = included;
        BillingMode = billingMode;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>.
    /// </summary>
    private UsageQuota()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public QuotaResource Resource { get; private set; }

    public DateOnly PeriodStart { get; private set; }

    public DateOnly PeriodEnd { get; private set; }

    /// <summary>Franquia do período. Para WhatsApp, reflete o uso gratuito da Meta.</summary>
    public int Included { get; private set; }

    public int Consumed { get; private set; }

    /// <summary>Créditos comprados antecipadamente (F4.16).</summary>
    public int PurchasedExtra { get; private set; }

    /// <summary>Teto definido pelo estabelecimento, além da franquia.</summary>
    public int? TenantLimit { get; private set; }

    public ConsumptionBillingMode BillingMode { get; private set; }

    /// <summary>Total disponível: franquia mais créditos comprados.</summary>
    public int Available => Included + PurchasedExtra;

    /// <summary>Saldo restante.</summary>
    public int Remaining => Math.Max(0, Available - Consumed);

    /// <summary>Indica se o consumo já ultrapassou a franquia.</summary>
    public bool IsOverIncluded => Consumed > Included;

    public static UsageQuota Create(
        Guid tenantId,
        QuotaResource resource,
        DateOnly periodStart,
        DateOnly periodEnd,
        int included,
        ConsumptionBillingMode billingMode = ConsumptionBillingMode.Automatic)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (periodStart >= periodEnd)
        {
            throw new DomainRuleException("O início do período deve ser anterior ao fim.");
        }

        if (included < 0)
        {
            throw new DomainRuleException("A franquia não pode ser negativa.");
        }

        return new UsageQuota(
            Guid.NewGuid(), tenantId, resource, periodStart, periodEnd, included, billingMode);
    }

    /// <summary>
    /// Tenta consumir uma quantidade.
    /// Retorna falso quando o consumo ultrapassaria o limite disponível.
    /// </summary>
    public bool TryConsume(int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainRuleException("A quantidade consumida deve ser maior que zero.");
        }

        var limit = TenantLimit.HasValue
            ? Math.Min(Available, TenantLimit.Value)
            : Available;

        if (Consumed + quantity > limit)
        {
            return false;
        }

        Consumed += quantity;
        return true;
    }

    /// <summary>
    /// Adiciona créditos comprados antecipadamente (F4.16).
    /// Só deve ser chamado após o pagamento confirmado.
    /// </summary>
    public void AddExtra(int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainRuleException("A quantidade de créditos deve ser maior que zero.");
        }

        PurchasedExtra += quantity;
    }

    /// <summary>Define o teto de consumo do estabelecimento.</summary>
    public void SetTenantLimit(int? limit)
    {
        if (limit is < 0)
        {
            throw new DomainRuleException("O limite não pode ser negativo.");
        }

        TenantLimit = limit;
    }

    /// <summary>Altera a modalidade de cobrança.</summary>
    public void ChangeBillingMode(ConsumptionBillingMode mode) => BillingMode = mode;

    /// <summary>Reinicia o consumo para um novo período.</summary>
    public void Reset(DateOnly periodStart, DateOnly periodEnd, int included)
    {
        if (periodStart >= periodEnd)
        {
            throw new DomainRuleException("O início do período deve ser anterior ao fim.");
        }

        if (included < 0)
        {
            throw new DomainRuleException("A franquia não pode ser negativa.");
        }

        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        Included = included;
        Consumed = 0;
        PurchasedExtra = 0;
    }
}

/// <summary>Recurso medido.</summary>
public enum QuotaResource
{
    /// <summary>Mensagens de WhatsApp.</summary>
    WhatsAppMessages = 1,

    /// <summary>Unidades de consumo de IA.</summary>
    AiUnits = 2
}

/// <summary>Modalidade de cobrança do consumo.</summary>
public enum ConsumptionBillingMode
{
    /// <summary>Consumo apurado no período e cobrado na fatura seguinte.</summary>
    Automatic = 1,

    /// <summary>Créditos comprados antecipadamente.</summary>
    PrepaidCredits = 2
}
