using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Billing;

/// <summary>
/// Fatura da plataforma contra um estabelecimento (F4.3).
///
/// Pertence ao <b>billing da plataforma</b>, não ao financeiro do tenant
/// (ADR-020). Cada origem de cobrança é uma linha separada (F4.14), o que
/// permite ao tenant identificar exatamente o que está pagando.
///
/// O total é sempre a soma das linhas: não há valor avulso que não tenha
/// origem rastreável.
/// </summary>
public sealed class PlatformInvoice : ITenantScoped
{
    private readonly List<InvoiceLine> _lines = [];

    private PlatformInvoice(
        Guid id,
        Guid tenantId,
        Guid subscriptionId,
        DateOnly periodStart,
        DateOnly periodEnd,
        DateOnly dueDate,
        InvoiceStatus status)
    {
        Id = id;
        TenantId = tenantId;
        SubscriptionId = subscriptionId;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        DueDate = dueDate;
        Status = status;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>.
    /// </summary>
    private PlatformInvoice()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid SubscriptionId { get; private set; }

    public DateOnly PeriodStart { get; private set; }

    public DateOnly PeriodEnd { get; private set; }

    public DateOnly DueDate { get; private set; }

    public InvoiceStatus Status { get; private set; }

    public DateTimeOffset? PaidAt { get; private set; }

    public string? GatewayPaymentId { get; private set; }

    public IReadOnlyCollection<InvoiceLine> Lines => _lines;

    /// <summary>Total da fatura: soma das linhas.</summary>
    public Money Total => _lines.Count == 0
        ? Money.Zero()
        : _lines.Select(line => line.Amount).Aggregate((first, second) => first.Add(second));

    /// <summary>Indica se a fatura está paga.</summary>
    public bool IsPaid => Status == InvoiceStatus.Paid;

    /// <summary>Indica se a fatura está vencida na data informada.</summary>
    public bool IsOverdue(DateOnly today) =>
        Status is InvoiceStatus.Pending or InvoiceStatus.Failed && today > DueDate;

    public static PlatformInvoice Create(
        Guid tenantId,
        Guid subscriptionId,
        DateOnly periodStart,
        DateOnly periodEnd,
        DateOnly dueDate)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (subscriptionId == Guid.Empty)
        {
            throw new DomainRuleException("A assinatura é obrigatória.");
        }

        if (periodStart >= periodEnd)
        {
            throw new DomainRuleException("O início do período deve ser anterior ao fim.");
        }

        if (dueDate < periodEnd)
        {
            throw new DomainRuleException("O vencimento não pode ser anterior ao fim do período.");
        }

        return new PlatformInvoice(
            Guid.NewGuid(), tenantId, subscriptionId, periodStart, periodEnd, dueDate,
            InvoiceStatus.Pending);
    }

    /// <summary>
    /// Adiciona uma linha de cobrança.
    /// Cada origem tem sua própria linha, garantindo transparência (F4.14).
    /// </summary>
    public InvoiceLine AddLine(InvoiceLineType type, string description, Money amount)
    {
        if (IsPaid)
        {
            throw new DomainRuleException("Uma fatura paga não pode receber novas linhas.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new DomainRuleException("A descrição da linha é obrigatória.");
        }

        var line = new InvoiceLine(Guid.NewGuid(), type, description.Trim(), amount);
        _lines.Add(line);

        return line;
    }

    /// <summary>Marca a fatura como paga.</summary>
    public void MarkPaid(string gatewayPaymentId, DateTimeOffset now)
    {
        if (IsPaid)
        {
            throw new DomainRuleException("A fatura já está paga.");
        }

        if (string.IsNullOrWhiteSpace(gatewayPaymentId))
        {
            throw new DomainRuleException("O identificador do pagamento é obrigatório.");
        }

        Status = InvoiceStatus.Paid;
        PaidAt = now;
        GatewayPaymentId = gatewayPaymentId.Trim();
    }

    /// <summary>Marca a fatura como falha de cobrança.</summary>
    public void MarkFailed()
    {
        if (IsPaid)
        {
            throw new DomainRuleException("Uma fatura paga não pode ser marcada como falha.");
        }

        Status = InvoiceStatus.Failed;
    }

    /// <summary>Cancela a fatura.</summary>
    public void Cancel()
    {
        if (IsPaid)
        {
            throw new DomainRuleException("Uma fatura paga não pode ser cancelada.");
        }

        Status = InvoiceStatus.Cancelled;
    }
}

/// <summary>Linha de cobrança de uma fatura.</summary>
public sealed class InvoiceLine
{
    /// <summary>
    /// Construtor sem parametros exigido pelo EF Core para materializar a
    /// entidade. Nao valida invariantes: a validacao ocorre em AddLine.
    /// </summary>
    private InvoiceLine()
    {
        Description = string.Empty;
    }

    internal InvoiceLine(Guid id, InvoiceLineType type, string description, Money amount)
    {
        Id = id;
        Type = type;
        Description = description;
        Amount = amount;
    }

    public Guid Id { get; private set; }

    public InvoiceLineType Type { get; private set; }

    public string Description { get; private set; }

    public Money Amount { get; private set; }
}

/// <summary>Estado da fatura da plataforma.</summary>
public enum InvoiceStatus
{
    Pending = 1,
    Paid = 2,
    Failed = 3,
    Cancelled = 4
}
