using System.Security.Cryptography;
using System.Text;
using Servia.Domain.Operations;

namespace Servia.Infrastructure.Integrations;

/// <summary>
/// Validação da assinatura HMAC dos webhooks (F17.3).
///
/// Cada provedor assina o corpo com um segredo compartilhado. Sem essa
/// verificação, qualquer origem poderia forjar um evento — por exemplo,
/// marcar uma fatura como paga.
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
    public bool IsValid(string source, string rawBody, string? signature, string? requestId)
    {
        if (string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(rawBody))
        {
            return false;
        }

        var secret = ResolveSecret(source);

        if (string.IsNullOrWhiteSpace(secret))
        {
            // Sem segredo, nao ha como validar: rejeita.
            return false;
        }

        var expected = ComputeSignature(source, secret, rawBody, requestId);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant()),
            Encoding.UTF8.GetBytes(expected));
    }

    private string? ResolveSecret(string source) => source switch
    {
        "mercadopago" => mercadoPago.WebhookSecret,
        "whatsapp" => whatsApp.WebhookSecret,
        _ => null
    };

    /// <summary>
    /// Calcula a assinatura esperada.
    ///
    /// Mercado Pago usa `sha256=<hmac>` sobre `id:requestId:ts`.
    /// Meta usa `sha256=<hmac>` sobre o corpo bruto.
    /// </summary>
    private static string ComputeSignature(
        string source,
        string secret,
        string rawBody,
        string? requestId)
    {
        var payload = source switch
        {
            "mercadopago" => $"{requestId}:{rawBody}",
            _ => rawBody
        };

        var hash = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes(payload));

        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
