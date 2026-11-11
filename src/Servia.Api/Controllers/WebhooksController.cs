using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Servia.Api.Contracts;
using Servia.Application.Webhooks;
using Servia.Domain.Operations;
using Servia.Domain.Tenancy;

namespace Servia.Api.Controllers;

/// <summary>
/// Recepção de webhooks dos provedores (F17).
///
/// As integrações foram implementadas como clientes; este controller
/// fecha o outro lado. Sem ele, o Servia nunca sabe que um pagamento foi
/// aprovado ou que uma mensagem chegou.
///
/// <b>Autenticação (F17.3):</b> webhooks não usam JWT — o provedor não
/// tem token. A autenticidade vem da assinatura HMAC calculada com o
/// segredo compartilhado. Assinatura inválida é rejeitada com 401.
///
/// <b>Tenant:</b> o provedor não conhece o tenant. Ele é resolvido pelo
/// identificador que o próprio Servia enviou ao criar a cobrança ou a
/// conversa — nunca por um header que o cliente controla.
/// </summary>
[ApiController]
[Route("api/v1/webhooks")]
[AllowAnonymous]
public sealed class WebhooksController(
    WebhookService webhooks,
    IWebhookSignatureValidator signatures,
    IWebhookTenantResolver tenants) : ControllerBase
{
    /// <summary>Recebe eventos de pagamento do Mercado Pago (F17.1).</summary>
    [HttpPost("mercadopago")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MercadoPagoAsync(
        [FromBody] JsonElement payload,
        [FromHeader(Name = "x-signature")] string? signature,
        [FromHeader(Name = "x-request-id")] string? requestId,
        CancellationToken cancellationToken)
    {
        var raw = payload.GetRawText();

        if (!signatures.IsValid("mercadopago", raw, signature, requestId))
        {
            return Unauthorized(new ErrorResponse("Assinatura do webhook inválida."));
        }

        var parsed = ParsePayment(payload);

        if (parsed is null)
        {
            // Evento desconhecido: registra e responde 200 para o provedor
            // nao reenviar indefinidamente (F17.5).
            return Ok(new { outcome = "ignored", reason = "Evento não reconhecido." });
        }

        var tenantId = await tenants.ResolveByPaymentAsync(
            parsed.GatewayPaymentId, cancellationToken);

        if (tenantId is null)
        {
            return Ok(new { outcome = "ignored", reason = "Pagamento não pertence a nenhum estabelecimento." });
        }

        var result = await webhooks.HandlePaymentAsync(tenantId.Value, parsed, cancellationToken);

        return result.Outcome == WebhookOutcome.Rejected
            ? BadRequest(new ErrorResponse(result.Reason!))
            : Ok(new { outcome = result.Outcome.ToString(), reason = result.Reason });
    }

    /// <summary>Recebe mensagens do WhatsApp (F17.2).</summary>
    [HttpPost("whatsapp")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> WhatsAppAsync(
        [FromBody] JsonElement payload,
        [FromHeader(Name = "x-hub-signature-256")] string? signature,
        CancellationToken cancellationToken)
    {
        var raw = payload.GetRawText();

        if (!signatures.IsValid("whatsapp", raw, signature, null))
        {
            return Unauthorized(new ErrorResponse("Assinatura do webhook inválida."));
        }

        var parsed = ParseMessage(payload);

        if (parsed is null)
        {
            return Ok(new { outcome = "ignored", reason = "Evento não reconhecido." });
        }

        var tenantId = await tenants.ResolveByPhoneNumberAsync(
            parsed.From, cancellationToken);

        if (tenantId is null)
        {
            return Ok(new { outcome = "ignored", reason = "Número não pertence a nenhum estabelecimento." });
        }

        var result = await webhooks.HandleIncomingMessageAsync(
            tenantId.Value, parsed, cancellationToken);

        return result.Outcome == WebhookOutcome.Rejected
            ? BadRequest(new ErrorResponse(result.Reason!))
            : Ok(new { outcome = result.Outcome.ToString(), reason = result.Reason });
    }

    /// <summary>
    /// Extrai o evento de pagamento do formato do Mercado Pago.
    /// Retorna nulo quando o evento não é de pagamento.
    /// </summary>
    private static PaymentWebhookEvent? ParsePayment(JsonElement payload)
    {
        if (!payload.TryGetProperty("type", out var type)
            || !string.Equals(type.GetString(), "payment", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!payload.TryGetProperty("data", out var data)
            || !data.TryGetProperty("id", out var id))
        {
            return null;
        }

        var eventId = payload.TryGetProperty("id", out var eventIdElement)
            ? eventIdElement.GetString() ?? id.ToString()
            : id.ToString();

        var status = data.TryGetProperty("status", out var statusElement)
            ? statusElement.GetString() ?? "unknown"
            : "unknown";

        return new PaymentWebhookEvent(eventId, id.ToString(), status);
    }

    /// <summary>
    /// Extrai a mensagem recebida do formato da Meta.
    /// Retorna nulo quando o evento não é de mensagem.
    /// </summary>
    private static IncomingMessageWebhookEvent? ParseMessage(JsonElement payload)
    {
        if (!payload.TryGetProperty("entry", out var entries)
            || entries.ValueKind != JsonValueKind.Array
            || entries.GetArrayLength() == 0)
        {
            return null;
        }

        var entry = entries[0];

        if (!entry.TryGetProperty("changes", out var changes)
            || changes.ValueKind != JsonValueKind.Array
            || changes.GetArrayLength() == 0)
        {
            return null;
        }

        var value = changes[0].TryGetProperty("value", out var valueElement)
            ? valueElement
            : default;

        if (value.ValueKind != JsonValueKind.Object
            || !value.TryGetProperty("messages", out var messages)
            || messages.ValueKind != JsonValueKind.Array
            || messages.GetArrayLength() == 0)
        {
            return null;
        }

        var message = messages[0];

        var from = message.TryGetProperty("from", out var fromElement)
            ? fromElement.GetString()
            : null;

        var body = message.TryGetProperty("text", out var text)
                   && text.TryGetProperty("body", out var bodyElement)
            ? bodyElement.GetString()
            : null;

        var externalId = message.TryGetProperty("id", out var idElement)
            ? idElement.GetString()
            : null;

        if (from is null || body is null)
        {
            return null;
        }

        return new IncomingMessageWebhookEvent(externalId ?? Guid.NewGuid().ToString(), from, body, externalId);
    }
}
