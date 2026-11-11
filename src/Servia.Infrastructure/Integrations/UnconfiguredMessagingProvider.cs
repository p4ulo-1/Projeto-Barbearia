using Servia.Domain.Messaging;

namespace Servia.Infrastructure.Integrations;

/// <summary>
/// Provedor de mensageria quando não há credencial configurada.
///
/// <b>Por que existe:</b> o `NotificationDispatchJob` depende de
/// `IMessagingProvider`. Sem credencial da Meta, o serviço não seria
/// registrado e a aplicação não subiria — mesmo com o WhatsApp
/// desabilitado, que é um estado válido.
///
/// <b>Comportamento:</b> toda tentativa de envio falha com motivo
/// explícito. A notificação é marcada como falha, não como enviada — o
/// operador vê no log que falta configuração, em vez de acreditar que a
/// mensagem saiu.
/// </summary>
public sealed class UnconfiguredMessagingProvider : IMessagingProvider
{
    private const string Reason =
        "Provedor de mensageria não configurado. Defina MetaWhatsApp:AccessToken.";

    public string ProviderName => "unconfigured";

    public Task<MessageSendResult> SendTextAsync(
        string phoneNumber,
        string body,
        string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(MessageSendResult.Failed(Reason));

    public Task<MessageSendResult> SendTemplateAsync(
        string phoneNumber,
        string templateName,
        IReadOnlyDictionary<string, string> parameters,
        string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(MessageSendResult.Failed(Reason));

    public Task<bool> IsTemplateApprovedAsync(
        string templateName,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
