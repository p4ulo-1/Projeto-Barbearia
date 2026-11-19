using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Servia.Api.Contracts;
using Servia.Application.Webhooks;
using Servia.Domain.Operations;
using Servia.Infrastructure.Repositories;

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
    IWebhookTenantResolver tenants,
    MercadoPagoInbox inbox,
    TimeProvider clock) : ControllerBase
{
    /// <summary>Recebe eventos de pagamento do Mercado Pago (F17.1).</summary>
    [HttpPost("mercadopago")]
    [RequestSizeLimit(16 * 1024)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> MercadoPagoAsync(
        [FromBody] JsonElement payload,
        [FromHeader(Name = "x-signature")] string? signature,
        [FromHeader(Name = "x-request-id")] string? requestId,
        CancellationToken cancellationToken)
    {
        var ids = Request.Query["data.id"];
        var resource = ids.Count == 1 ? ids[0] : null;
        if (!IsIdentifier(resource, 100)
            || signatures.ValidateMercadoPago(resource!, signature, requestId) is not { } timestamp)
        {
            return Unauthorized(new ErrorResponse("Assinatura do webhook inválida."));
        }
        if (payload.ValueKind != JsonValueKind.Object
            || !payload.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String
            || type.GetString() is not { } topic || !IsIdentifier(topic, 80)
            || !payload.TryGetProperty("id", out var id) || !IsIdentifier(ReadId(id), 100)
            || !payload.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object
            || !data.TryGetProperty("id", out var bodyId)
            || !string.Equals(resource, ReadId(bodyId), StringComparison.OrdinalIgnoreCase)
            || (Request.Query.TryGetValue("type", out var queryTopic) && (queryTopic.Count != 1 || queryTopic[0] != topic)))
        {
            return BadRequest(new ErrorResponse("Dados da notificação inválidos."));
        }

        if (topic is MercadoPagoNotification.Payment or MercadoPagoNotification.AuthorizedPayment
            && (!long.TryParse(resource, out var numericId) || numericId <= 0
                || numericId.ToString(System.Globalization.CultureInfo.InvariantCulture) != resource))
        {
            return BadRequest(new ErrorResponse("Identificador financeiro inválido."));
        }
        var supported = MercadoPagoNotification.IsSupported(topic);
        var now = clock.GetUtcNow();
        await inbox.EnqueueAsync(new MercadoPagoNotification
        {
            Id = Guid.NewGuid(), ExternalId = ReadId(id)!, Topic = topic,
            ResourceId = resource!.ToLowerInvariant(), RequestId = requestId!, SignatureTimestamp = timestamp,
            ReceivedAt = now, AvailableAt = now, CompletedAt = supported ? null : now,
            Result = supported ? null : "UnsupportedTopic"
        }, cancellationToken);
        return Ok(new { outcome = supported ? "accepted" : "ignored" });
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

        if (!signatures.IsValidWhatsApp(raw, signature))
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

    private static string? ReadId(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number when value.TryGetInt64(out _) => value.GetRawText(),
        _ => null
    };

    private static bool IsIdentifier(string? value, int limit) =>
        value is { Length: > 0 } && value.Length <= limit
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');

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
