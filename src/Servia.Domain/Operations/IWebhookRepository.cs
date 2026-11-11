using Servia.Domain.Billing;
using Servia.Domain.Messaging;
using Servia.Domain.Operations;

namespace Servia.Domain.Operations;

/// <summary>
/// Contrato de persistência dos webhooks (F17).
///
/// Definido no domínio para que a infraestrutura o implemente sem
/// depender da camada de aplicação (ADR-004).
/// </summary>
public interface IWebhookRepository
{
    /// <summary>Indica se o evento já foi processado.</summary>
    Task<bool> WasProcessedAsync(
        Guid tenantId,
        string eventId,
        string source,
        CancellationToken cancellationToken = default);

    /// <summary>Registra um evento processado.</summary>
    Task AddProcessedEventAsync(
        ProcessedEvent processedEvent,
        CancellationToken cancellationToken = default);

    /// <summary>Busca a fatura pelo identificador do pagamento no gateway.</summary>
    Task<PlatformInvoice?> FindInvoiceByGatewayIdAsync(
        Guid tenantId,
        string gatewayPaymentId,
        CancellationToken cancellationToken = default);

    /// <summary>Busca a conversa aberta do número informado.</summary>
    Task<Conversation?> FindOpenConversationAsync(
        Guid tenantId,
        string phoneNumber,
        CancellationToken cancellationToken = default);

    /// <summary>Registra uma nova conversa.</summary>
    Task AddConversationAsync(
        Conversation conversation,
        CancellationToken cancellationToken = default);

    /// <summary>Persiste as alterações pendentes.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
