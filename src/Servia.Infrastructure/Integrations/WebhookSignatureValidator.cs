using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using Servia.Domain.Operations;

namespace Servia.Infrastructure.Integrations;

/// <summary>
/// Validação da assinatura HMAC dos webhooks (F17.3).
///
/// Cada provedor tem um manifesto próprio autenticado por segredo compartilhado.
///
/// A comparação usa <see cref="CryptographicOperations.FixedTimeEquals"/>:
/// comparar com `==` vazaria informação por tempo de resposta.
///
/// <b>Sem segredo configurado, rejeita tudo.</b> Falhar fechado é
/// obrigatório em fronteira de confiança.
/// </summary>
public sealed class WebhookSignatureValidator(
    MercadoPagoOptions mercadoPago,
    MetaWhatsAppOptions whatsApp) : IWebhookSignatureValidator
{
    public bool IsValidWhatsApp(string rawBody, string? signature)
    {
        if (string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(rawBody))
        {
            return false;
        }

        var secret = whatsApp.WebhookSecret;

        if (string.IsNullOrWhiteSpace(secret))
        {
            // Sem segredo, nao ha como validar: rejeita.
            return false;
        }

        var hash = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes(rawBody));
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant()),
            Encoding.UTF8.GetBytes(Convert.ToHexStringLower(hash)));
    }

    public long? ValidateMercadoPago(string resourceId, string? signature, string? requestId)
    {
        if (string.IsNullOrWhiteSpace(mercadoPago.WebhookSecret)
            || signature is not { Length: > 0 and <= 512 }
            || requestId is not { Length: > 0 and <= 128 }
            || !requestId.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.'))
        {
            return null;
        }

        string? timestamp = null;
        string? digest = null;
        foreach (var part in signature.Split(','))
        {
            var field = part.Split('=', 2, StringSplitOptions.TrimEntries);
            if (field.Length != 2) return null;
            switch (field[0])
            {
                case "ts" when timestamp is null: timestamp = field[1]; break;
                case "v1" when digest is null: digest = field[1]; break;
                case "ts" or "v1": return null;
            }
        }
        if (timestamp is not { Length: 10 or 13 }
            || !long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var unix)
            || digest is not { Length: 64 })
        {
            return null;
        }
        Span<byte> received = stackalloc byte[32];
        if (Convert.FromHexString(digest, received, out _, out _) != System.Buffers.OperationStatus.Done)
        {
            return null;
        }
        var manifest = $"id:{resourceId.ToLowerInvariant()};request-id:{requestId};ts:{timestamp};";
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(mercadoPago.WebhookSecret),
            Encoding.UTF8.GetBytes(manifest));
        return CryptographicOperations.FixedTimeEquals(received, expected) ? unix : null;
    }
}
