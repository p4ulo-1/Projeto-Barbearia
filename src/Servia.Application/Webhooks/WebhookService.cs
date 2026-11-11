using Servia.Domain.Billing;
using Servia.Domain.Common;
using Servia.Domain.Messaging;
using Servia.Domain.Operations;

namespace Servia.Application.Webhooks;

/// <summary>
/// Casos de uso de webhook (F17).
///
/// As integrações foram implementadas como clientes — o Servia chama o
/// provedor. Este serviço fecha o outro lado: receber o evento que o
/// provedor envia quando algo acontece.
///
/// <b>Idempotência (F17.4):</b> todo evento é registrado por
/// `(tenantId, eventId, source)`. Um reenvio do provedor não gera
/// cobrança nem mensagem duplicada.
///
/// <b>Evento desconhecido (F17.5):</b> é registrado e ignorado, sem erro.
/// O provedor pode adicionar tipos novos a qualquer momento; falhar com
/// 500 faria ele reenviar para sempre.
/// </summary>
public sealed class WebhookService(
    IWebhookRepository webhooks,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Processa um evento de pagamento (F17.1).
    /// </summary>
    public async Task<WebhookResult> HandlePaymentAsync(
        Guid tenantId,
        PaymentWebhookEvent payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (tenantId == Guid.Empty)
        {
            return WebhookResult.Rejected("Estabelecimento não identificado.");
        }

        if (string.IsNullOrWhiteSpace(payload.EventId))
        {
            return WebhookResult.Rejected("Evento sem identificador.");
        }

        var now = timeProvider.GetUtcNow();

        if (await webhooks.WasProcessedAsync(tenantId, payload.EventId, "mercadopago", cancellationToken))
        {
            return WebhookResult.Duplicate();
        }

        var invoice = await webhooks.FindInvoiceByGatewayIdAsync(
            tenantId, payload.GatewayPaymentId, cancellationToken);

        if (invoice is null)
        {
            // Evento de um pagamento que nao conhecemos: registra e ignora.
            await RecordAsync(tenantId, payload.EventId, "mercadopago", now, cancellationToken);
            return WebhookResult.Ignored("Pagamento não corresponde a nenhuma fatura.");
        }

        try
        {
            if (payload.IsApproved)
            {
                invoice.MarkPaid(payload.GatewayPaymentId, now);
            }
            else if (payload.IsRejected)
            {
                invoice.MarkFailed();
            }
            else
            {
                // Status intermediario: registra e nao altera a fatura.
                await RecordAsync(tenantId, payload.EventId, "mercadopago", now, cancellationToken);
                return WebhookResult.Ignored($"Status '{payload.Status}' não altera a fatura.");
            }
        }
        catch (DomainRuleException exception)
        {
            return WebhookResult.Rejected(exception.Message);
        }

        await RecordAsync(tenantId, payload.EventId, "mercadopago", now, cancellationToken);
        await webhooks.SaveChangesAsync(cancellationToken);

        return WebhookResult.Processed();
    }

    /// <summary>
    /// Processa um evento de mensagem recebida (F17.2).
    /// </summary>
    public async Task<WebhookResult> HandleIncomingMessageAsync(
        Guid tenantId,
        IncomingMessageWebhookEvent payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (tenantId == Guid.Empty)
        {
            return WebhookResult.Rejected("Estabelecimento não identificado.");
        }

        if (string.IsNullOrWhiteSpace(payload.EventId))
        {
            return WebhookResult.Rejected("Evento sem identificador.");
        }

        var now = timeProvider.GetUtcNow();

        if (await webhooks.WasProcessedAsync(tenantId, payload.EventId, "whatsapp", cancellationToken))
        {
            return WebhookResult.Duplicate();
        }

        if (string.IsNullOrWhiteSpace(payload.From) || string.IsNullOrWhiteSpace(payload.Body))
        {
            await RecordAsync(tenantId, payload.EventId, "whatsapp", now, cancellationToken);
            return WebhookResult.Ignored("Mensagem sem remetente ou corpo.");
        }

        var conversation = await webhooks.FindOpenConversationAsync(
            tenantId, payload.From, cancellationToken);

        if (conversation is null)
        {
            conversation = Conversation.Start(tenantId, payload.From, now);
            await webhooks.AddConversationAsync(conversation, cancellationToken);
        }

        try
        {
            conversation.ReceiveMessage(payload.Body, now, payload.ExternalId);
        }
        catch (DomainRuleException exception)
        {
            return WebhookResult.Rejected(exception.Message);
        }

        await RecordAsync(tenantId, payload.EventId, "whatsapp", now, cancellationToken);
        await webhooks.SaveChangesAsync(cancellationToken);

        return WebhookResult.Processed();
    }

    private async Task RecordAsync(
        Guid tenantId,
        string eventId,
        string source,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var processed = ProcessedEvent.Record(tenantId, eventId, source, now);

        await webhooks.AddProcessedEventAsync(processed, cancellationToken);
        await webhooks.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Resultado do processamento de um webhook.</summary>
public sealed record WebhookResult(WebhookOutcome Outcome, string? Reason)
{
    public static WebhookResult Processed() => new(WebhookOutcome.Processed, null);

    public static WebhookResult Duplicate() =>
        new(WebhookOutcome.Duplicate, "Evento já processado.");

    public static WebhookResult Ignored(string reason) => new(WebhookOutcome.Ignored, reason);

    public static WebhookResult Rejected(string reason) => new(WebhookOutcome.Rejected, reason);
}

/// <summary>Desfecho do processamento.</summary>
public enum WebhookOutcome
{
    /// <summary>Evento aplicado com sucesso.</summary>
    Processed = 1,

    /// <summary>Evento já processado antes: reenvio do provedor.</summary>
    Duplicate = 2,

    /// <summary>Evento válido, mas sem efeito no estado atual.</summary>
    Ignored = 3,

    /// <summary>Evento inválido ou que viola uma regra de negócio.</summary>
    Rejected = 4
}

/// <summary>Evento de pagamento recebido do provedor.</summary>
public sealed record PaymentWebhookEvent(
    string EventId,
    string GatewayPaymentId,
    string Status)
{
    public bool IsApproved => string.Equals(Status, "approved", StringComparison.OrdinalIgnoreCase);

    public bool IsRejected => string.Equals(Status, "rejected", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Evento de mensagem recebida do provedor.</summary>
public sealed record IncomingMessageWebhookEvent(
    string EventId,
    string From,
    string Body,
    string? ExternalId);
