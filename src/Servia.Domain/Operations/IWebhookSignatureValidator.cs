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
    bool IsValid(string source, string rawBody, string? signature, string? requestId);
}

/// <summary>
/// Resolve o tenant de um webhook (F17).
///
/// O provedor não conhece o tenant: ele é descoberto pelo identificador
/// que o Servia registrou ao criar a cobrança ou a conversa.
/// </summary>
public interface IWebhookTenantResolver
{
    /// <summary>Resolve o tenant pelo identificador do pagamento no gateway.</summary>
    Task<Guid?> ResolveByPaymentAsync(
        string gatewayPaymentId,
        CancellationToken cancellationToken = default);

    /// <summary>Resolve o tenant pelo número de telefone da conversa.</summary>
    Task<Guid?> ResolveByPhoneNumberAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default);
}
