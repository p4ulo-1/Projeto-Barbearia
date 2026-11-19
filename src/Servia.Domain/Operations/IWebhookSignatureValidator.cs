namespace Servia.Domain.Operations;

/// <summary>
/// Validação da assinatura de webhook (F17.3).
///
/// Sem isso, qualquer origem poderia forjar um evento e, por exemplo,
/// marcar uma fatura como paga.
/// </summary>
public interface IWebhookSignatureValidator
{
    /// <summary>
    /// Indica se a assinatura corresponde ao corpo enviado.
    /// </summary>
    bool IsValidWhatsApp(string rawBody, string? signature);

    long? ValidateMercadoPago(string resourceId, string? signature, string? requestId);
}

/// <summary>
/// Resolve o tenant de um webhook de mensagens pelo telefone da conversa.
/// </summary>
public interface IWebhookTenantResolver
{
    /// <summary>Resolve o tenant pelo número de telefone da conversa.</summary>
    Task<Guid?> ResolveByPhoneNumberAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default);
}
