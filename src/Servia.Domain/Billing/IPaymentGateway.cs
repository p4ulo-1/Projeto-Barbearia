using Servia.Domain.Common;

namespace Servia.Domain.Billing;

/// <summary>
/// Contrato de gateway de pagamento (F4.1).
///
/// Abstrai o provedor para que os casos de uso não dependam de SDK nem de
/// detalhes do Mercado Pago (ADR-005). Trocar de provedor é implementar este
/// contrato, sem tocar na aplicação.
///
/// Todas as operações são <b>idempotentes</b> por chave de idempotência: uma
/// repetição de webhook ou de requisição não gera cobrança duplicada (F4.4).
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Identifica o provedor, para logs e auditoria.</summary>
    string ProviderName { get; }

    /// <summary>
    /// Registra um cartão de crédito para cobrança recorrente (F4.2).
    /// </summary>
    Task<GatewayCustomer> RegisterCustomerAsync(
        Guid tenantId,
        string email,
        string cardToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cria uma assinatura recorrente no gateway (F4.5).
    /// </summary>
    Task<GatewaySubscription> CreateSubscriptionAsync(
        string gatewayCustomerId,
        Money amount,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cria uma cobrança avulsa, usada para consumo excedente (F4.17).
    /// </summary>
    Task<GatewayCharge> CreateChargeAsync(
        string gatewayCustomerId,
        Money amount,
        string description,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Estorna uma cobrança, total ou parcialmente.
    /// </summary>
    Task<GatewayRefund> RefundAsync(
        string gatewayPaymentId,
        Money amount,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancela uma assinatura recorrente.
    /// </summary>
    Task CancelSubscriptionAsync(
        string gatewaySubscriptionId,
        CancellationToken cancellationToken = default);
}

/// <summary>Cliente criado no gateway, com o cartão já registrado.</summary>
public sealed record GatewayCustomer(string CustomerId, string? CardLastFour, string? CardBrand);

/// <summary>Assinatura criada no gateway.</summary>
public sealed record GatewaySubscription(string SubscriptionId, GatewaySubscriptionStatus Status);

/// <summary>Cobrança criada no gateway.</summary>
public sealed record GatewayCharge(
    string PaymentId,
    GatewayChargeStatus Status,
    string? PaymentUrl);

/// <summary>Resultado de um estorno.</summary>
public sealed record GatewayRefund(string RefundId, Money Amount);

/// <summary>Estado da assinatura no gateway.</summary>
public enum GatewaySubscriptionStatus
{
    Pending = 1,
    Active = 2,
    PastDue = 3,
    Cancelled = 4
}

/// <summary>Estado da cobrança no gateway.</summary>
public enum GatewayChargeStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Refunded = 4
}

/// <summary>
/// Evento recebido por webhook do gateway (F4.4).
///
/// O <see cref="EventId"/> é a chave de idempotência: o mesmo evento
/// recebido duas vezes é processado uma única vez.
/// </summary>
public sealed record GatewayWebhookEvent(
    string EventId,
    string EventType,
    string PaymentId,
    GatewayChargeStatus Status,
    Money Amount,
    DateTimeOffset OccurredAt);
