namespace Servia.Domain.Messaging;

/// <summary>
/// Contrato de provedor de mensageria (F6.1).
///
/// Abstrai a Cloud API da Meta para que os casos de uso não dependam de SDK
/// nem de detalhes do provedor (ADR-006). Trocar de provedor é implementar
/// este contrato, sem tocar na aplicação.
///
/// O envio é <b>idempotente</b> por chave: uma repetição não gera mensagem
/// duplicada ao cliente.
/// </summary>
public interface IMessagingProvider
{
    /// <summary>Identifica o provedor, para logs e auditoria.</summary>
    string ProviderName { get; }

    /// <summary>
    /// Envia uma mensagem de texto.
    /// </summary>
    Task<MessageSendResult> SendTextAsync(
        string phoneNumber,
        string body,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Envia uma mensagem usando template aprovado pelo provedor.
    /// Templates são obrigatórios fora da janela de 24 horas.
    /// </summary>
    Task<MessageSendResult> SendTemplateAsync(
        string phoneNumber,
        string templateName,
        IReadOnlyDictionary<string, string> parameters,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Valida se um template está aprovado e pode ser usado.
    /// </summary>
    Task<bool> IsTemplateApprovedAsync(
        string templateName,
        CancellationToken cancellationToken = default);
}

/// <summary>Resultado do envio de uma mensagem.</summary>
public sealed record MessageSendResult(
    bool Success,
    string? ExternalId,
    string? FailureReason)
{
    public static MessageSendResult Sent(string externalId) => new(true, externalId, null);

    public static MessageSendResult Failed(string reason) => new(false, null, reason);
}

/// <summary>
/// Evento recebido por webhook do provedor (F6.1).
///
/// O <see cref="EventId"/> é a chave de idempotência: o mesmo evento
/// recebido duas vezes é processado uma única vez.
/// </summary>
public sealed record InboundMessageEvent(
    string EventId,
    string FromPhoneNumber,
    string Body,
    DateTimeOffset ReceivedAt);
