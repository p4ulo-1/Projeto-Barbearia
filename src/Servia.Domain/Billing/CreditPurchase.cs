using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Billing;

/// <summary>
/// Compra antecipada de créditos de consumo (F4.16).
///
/// O estabelecimento compra créditos antes de consumir, em vez de receber a
/// cobrança no fim do período. Os créditos só são aplicados à cota após o
/// pagamento confirmado — comprar não é o mesmo que pagar.
/// </summary>
public sealed class CreditPurchase : ITenantScoped
{
    private CreditPurchase(
        Guid id,
        Guid tenantId,
        QuotaResource resource,
        int quantity,
        Money amount,
        DateTimeOffset purchasedAt)
    {
        Id = id;
        TenantId = tenantId;
        Resource = resource;
        Quantity = quantity;
        Amount = amount;
        PurchasedAt = purchasedAt;
        Status = CreditPurchaseStatus.Pending;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>.
    /// </summary>
    private CreditPurchase()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public QuotaResource Resource { get; private set; }

    public int Quantity { get; private set; }

    public Money Amount { get; private set; }

    public DateTimeOffset PurchasedAt { get; private set; }

    public CreditPurchaseStatus Status { get; private set; }

    public Guid? PlatformInvoiceId { get; private set; }

    /// <summary>Indica se os créditos já podem ser aplicados à cota.</summary>
    public bool IsPaid => Status == CreditPurchaseStatus.Paid;

    public static CreditPurchase Create(
        Guid tenantId,
        QuotaResource resource,
        int quantity,
        Money amount,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (quantity <= 0)
        {
            throw new DomainRuleException("A quantidade de créditos deve ser maior que zero.");
        }

        return new CreditPurchase(Guid.NewGuid(), tenantId, resource, quantity, amount, now);
    }

    /// <summary>
    /// Confirma o pagamento, vinculando a compra à fatura.
    /// A partir daqui os créditos podem ser aplicados à cota.
    /// </summary>
    public void MarkPaid(Guid platformInvoiceId)
    {
        if (IsPaid)
        {
            throw new DomainRuleException("A compra já está paga.");
        }

        if (platformInvoiceId == Guid.Empty)
        {
            throw new DomainRuleException("A fatura é obrigatória.");
        }

        Status = CreditPurchaseStatus.Paid;
        PlatformInvoiceId = platformInvoiceId;
    }

    /// <summary>Cancela a compra antes do pagamento.</summary>
    public void Cancel()
    {
        if (IsPaid)
        {
            throw new DomainRuleException("Uma compra paga não pode ser cancelada.");
        }

        Status = CreditPurchaseStatus.Cancelled;
    }

    /// <summary>
    /// Aplica os créditos à cota. Exige pagamento confirmado.
    /// </summary>
    public void ApplyTo(UsageQuota quota)
    {
        ArgumentNullException.ThrowIfNull(quota);

        if (!IsPaid)
        {
            throw new DomainRuleException("Os créditos só podem ser aplicados após o pagamento.");
        }

        if (quota.Resource != Resource)
        {
            throw new DomainRuleException("Os créditos são de um recurso diferente da cota.");
        }

        quota.AddExtra(Quantity);
    }
}

/// <summary>Estado da compra de créditos.</summary>
public enum CreditPurchaseStatus
{
    Pending = 1,
    Paid = 2,
    Cancelled = 3
}
