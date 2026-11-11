using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Servia.Domain.Common;
using Servia.Domain.Messaging;

namespace Servia.Infrastructure.Integrations;

/// <summary>
/// Provedor de WhatsApp via Cloud API da Meta (F12.2).
///
/// Implementa <see cref="IMessagingProvider"/> sobre a API oficial. O envio
/// é idempotente por chave: repetir não gera mensagem duplicada ao cliente
/// (F12.5).
///
/// Fora da janela de 24 horas, a Meta exige template aprovado. O provedor
/// não tenta contornar isso: usa template quando o texto livre não é
/// permitido.
/// </summary>
public sealed class MetaWhatsAppProvider : IMessagingProvider
{
    private const string ApiBaseUrl = "https://graph.facebook.com/v21.0";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly MetaWhatsAppOptions _options;
    private readonly RetryPolicy _retryPolicy;

    public MetaWhatsAppProvider(
        HttpClient httpClient,
        MetaWhatsAppOptions options,
        RetryPolicy? retryPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.AccessToken))
        {
            throw new InvalidOperationException(
                "O token de acesso da Meta é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(options.PhoneNumberId))
        {
            throw new InvalidOperationException(
                "O identificador do número de telefone é obrigatório.");
        }

        _httpClient = httpClient;
        _options = options;
        _retryPolicy = retryPolicy ?? new RetryPolicy();

        _httpClient.BaseAddress ??= new Uri(ApiBaseUrl);
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.AccessToken);
    }

    public string ProviderName => "meta-whatsapp";

    public async Task<MessageSendResult> SendTextAsync(
        string phoneNumber,
        string body,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            throw new DomainRuleException("O telefone é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new DomainRuleException("O corpo da mensagem é obrigatório.");
        }

        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = phoneNumber,
            type = "text",
            text = new { body }
        };

        return await SendAsync(payload, idempotencyKey, cancellationToken);
    }

    public async Task<MessageSendResult> SendTemplateAsync(
        string phoneNumber,
        string templateName,
        IReadOnlyDictionary<string, string> parameters,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        if (string.IsNullOrWhiteSpace(templateName))
        {
            throw new DomainRuleException("O nome do template é obrigatório.");
        }

        var payload = new
        {
            messaging_product = "whatsapp",
            to = phoneNumber,
            type = "template",
            template = new
            {
                name = templateName,
                language = new { code = _options.TemplateLanguage },
                components = new[]
                {
                    new
                    {
                        type = "body",
                        parameters = parameters
                            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                            .Select(entry => new { type = "text", text = entry.Value })
                            .ToArray()
                    }
                }
            }
        };

        return await SendAsync(payload, idempotencyKey, cancellationToken);
    }

    public async Task<bool> IsTemplateApprovedAsync(
        string templateName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(templateName))
        {
            return false;
        }

        var url = $"/{_options.BusinessAccountId}/message_templates?name={Uri.EscapeDataString(templateName)}";

        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var body = await response.Content.ReadFromJsonAsync<TemplateListResponse>(
                JsonOptions, cancellationToken);

            return body?.Data?.Any(template =>
                string.Equals(template.Name, templateName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(template.Status, "APPROVED", StringComparison.OrdinalIgnoreCase)) == true;
        }
        catch (HttpRequestException)
        {
            // Falha de rede: nao afirma que o template esta aprovado.
            return false;
        }
    }

    private async Task<MessageSendResult> SendAsync(
        object payload,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var url = $"/{_options.PhoneNumberId}/messages";

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            request.Headers.Add("X-Idempotency-Key", idempotencyKey);
        }

        try
        {
            var response = await _retryPolicy.ExecuteAsync(
                token => _httpClient.SendAsync(request, token),
                result => RetryPolicy.IsTransient((int)result.StatusCode),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync(cancellationToken);

                return MessageSendResult.Failed($"{(int)response.StatusCode}: {detail}");
            }

            var body = await response.Content.ReadFromJsonAsync<SendResponse>(
                JsonOptions, cancellationToken);

            var messageId = body?.Messages?.FirstOrDefault()?.Id;

            return messageId is null
                ? MessageSendResult.Failed("O provedor não retornou o identificador da mensagem.")
                : MessageSendResult.Sent(messageId);
        }
        catch (HttpRequestException exception)
        {
            return MessageSendResult.Failed(exception.Message);
        }
    }

    // ---------- Contratos de resposta do provedor ----------

    private sealed record SendResponse(List<SentMessage>? Messages);

    private sealed record SentMessage(string? Id);

    private sealed record TemplateListResponse(List<TemplateInfo>? Data);

    private sealed record TemplateInfo(string? Name, string? Status);
}

/// <summary>Configuração da Cloud API da Meta.</summary>
public sealed class MetaWhatsAppOptions
{
    /// <summary>Token de acesso. Deve vir de variável de ambiente ou cofre.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Identificador do número de telefone no provedor.</summary>
    public string PhoneNumberId { get; set; } = string.Empty;

    /// <summary>Identificador da conta comercial, usado na consulta de templates.</summary>
    public string BusinessAccountId { get; set; } = string.Empty;

    /// <summary>Idioma dos templates aprovados.</summary>
    public string TemplateLanguage { get; set; } = "pt_BR";

    /// <summary>Segredo do webhook, usado para validar a origem.</summary>
    public string WebhookSecret { get; set; } = string.Empty;
}
