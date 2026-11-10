using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Finance;

/// <summary>
/// Pagamento recebido pelo estabelecimento (F5.1, F5.2, F5.5).
///
/// Pertence ao <b>financeiro do tenant</b>, não ao billing da plataforma
/// (ADR-020). Registra o que o cliente pagou pelo serviço, seja presencial
/// ou online.
///
/// O estorno é parcial ou total e sempre deixa rastro: o valor estornado é
/// acumulado, e o status reflete o resultado.
/// </summary>
public sealed class Payment : ITenantScoped
{
    private Payment(
        Guid id,
        Guid tenantId,
        Guid? appointmentId,
        Money amount,
        PaymentMethod method,
        PaymentStatus status,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        AppointmentId = appointmentId;
        Amount = amount;
        Method = method;
        Status = status;
        CreatedAt = createdAt;

        // Money e um record struct: o valor padrao tem moeda nula.
        // Inicializa com a moeda do pagamento para permitir somas.
        RefundedAmount = Money.Zero(amount.Currency);
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>.
    /// </summary>
    private Payment()
    {
        RefundedAmount = Money.Zero();
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>Atendimento que originou o pagamento, quando houver.</summary>
    public Guid? AppointmentId { get; private set; }

    public Money Amount { get; private set; }

    public PaymentMethod Method { get; private set; }

    public PaymentStatus Status { get; private set; }

    /// <summary>Valor já estornado.</summary>
    public Money RefundedAmount { get; private set; }

    /// <summary>Identificador da cobrança no gateway, quando online.</summary>
    public string? GatewayPaymentId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? PaidAt { get; private set; }

    /// <summary>Indica se o pagamento foi confirmado.</summary>
    public bool IsPaid => Status is PaymentStatus.Paid or PaymentStatus.PartiallyRefunded;

    /// <summary>Valor líquido após estornos.</summary>
    public Money NetAmount => Amount.Subtract(RefundedAmount);

    /// <summary>
    /// Registra um pagamento presencial (F5.1).
    /// </summary>
    public static Payment RegisterInPerson(
        Guid tenantId,
        Guid? appointmentId,
        Money amount,
        PaymentMethod method,
        DateTimeOffset now)
    {
        // Pagamento presencial é confirmado no ato: qualquer forma é aceita.
        return Create(tenantId, appointmentId, amount, method, PaymentStatus.Paid, now);
    }

    /// <summary>
    /// Registra um pagamento online pendente de confirmação (F5.2).
    /// O status só muda quando o webhook do gateway confirma.
    /// </summary>
    public static Payment RegisterOnline(
        Guid tenantId,
        Guid? appointmentId,
        Money amount,
        PaymentMethod method,
        string gatewayPaymentId,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(gatewayPaymentId))
        {
            throw new DomainRuleException("O identificador do gateway é obrigatório.");
        }

        var payment = Create(tenantId, appointmentId, amount, method, PaymentStatus.Pending, now);
        payment.GatewayPaymentId = gatewayPaymentId.Trim();

        return payment;
    }

    private static Payment Create(
        Guid tenantId,
        Guid? appointmentId,
        Money amount,
        PaymentMethod method,
        PaymentStatus status,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (amount.Amount <= 0)
        {
            throw new DomainRuleException("O valor do pagamento deve ser maior que zero.");
        }

        var payment = new Payment(
            Guid.NewGuid(), tenantId, appointmentId, amount, method, status, now);

        if (status == PaymentStatus.Paid)
        {
            payment.PaidAt = now;
        }

        return payment;
    }

    /// <summary>
    /// Confirma um pagamento online, a partir do webhook do gateway (F5.2).
    /// </summary>
    public void Confirm(DateTimeOffset now)
    {
        if (Status != PaymentStatus.Pending)
        {
            throw new DomainRuleException("Somente pagamentos pendentes podem ser confirmados.");
        }

        Status = PaymentStatus.Paid;
        PaidAt = now;
    }

    /// <summary>Marca o pagamento como falho.</summary>
    public void Fail()
    {
        if (Status != PaymentStatus.Pending)
        {
            throw new DomainRuleException("Somente pagamentos pendentes podem falhar.");
        }

        Status = PaymentStatus.Failed;
    }

    /// <summary>
    /// Estorna total ou parcialmente o pagamento (F5.5).
    /// </summary>
    public void Refund(Money amount)
    {
        if (!IsPaid)
        {
            throw new DomainRuleException("Somente pagamentos confirmados podem ser estornados.");
        }

        if (amount.Amount <= 0)
        {
            throw new DomainRuleException("O valor do estorno deve ser maior que zero.");
        }

        var newTotal = RefundedAmount.Add(amount);

        if (newTotal.Amount > Amount.Amount)
        {
            throw new DomainRuleException("O estorno não pode exceder o valor pago.");
        }

        RefundedAmount = newTotal;

        Status = newTotal.Amount == Amount.Amount
            ? PaymentStatus.Refunded
            : PaymentStatus.PartiallyRefunded;
    }
}

/// <summary>Forma de pagamento.</summary>
public enum PaymentMethod
{
    Cash = 1,
    CreditCard = 2,
    DebitCard = 3,
    Pix = 4,
    BankTransfer = 5,
    Other = 99
}

/// <summary>Estado do pagamento.</summary>
public enum PaymentStatus
{
    Pending = 1,
    Paid = 2,
    Failed = 3,
    PartiallyRefunded = 4,
    Refunded = 5
}
