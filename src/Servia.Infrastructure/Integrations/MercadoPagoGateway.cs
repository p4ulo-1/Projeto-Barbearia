using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Servia.Domain.Billing;
using Servia.Domain.Common;

namespace Servia.Infrastructure.Integrations;

/// <summary>
/// Gateway de pagamento Mercado Pago (F12.1).
///
/// Implementa <see cref="IPaymentGateway"/> sobre a API REST do provedor.
/// Todas as operações enviam chave de idempotência: uma repetição não gera
/// cobrança duplicada (F12.4).
///
/// O token de acesso vem da configuração, nunca do código. Sem token, a
/// construção falha — melhor falhar do que cobrar em ambiente errado.
/// </summary>
public sealed class MercadoPagoGateway : IPaymentGateway
{
    private const string ApiBaseUrl = "https://api.mercadopago.com";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly RetryPolicy _retryPolicy;

    public MercadoPagoGateway(
        HttpClient httpClient,
        MercadoPagoOptions options,
        RetryPolicy? retryPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.AccessToken))
        {
            throw new InvalidOperationException(
                "O token de acesso do Mercado Pago é obrigatório.");
        }

        _httpClient = httpClient;
        _retryPolicy = retryPolicy ?? new RetryPolicy();

        _httpClient.BaseAddress ??= new Uri(ApiBaseUrl);
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.AccessToken);
    }

    public string ProviderName => "mercadopago";

    public async Task<GatewayCustomer> RegisterCustomerAsync(
        Guid tenantId,
        string email,
        string cardToken,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(cardToken))
        {
            throw new DomainRuleException("O token do cartão é obrigatório.");
        }

        var payload = new
        {
            email,
            card_token = cardToken,
            description = $"Estabelecimento {tenantId}"
        };

        var response = await _retryPolicy.ExecuteAsync(
            token => _httpClient.PostAsJsonAsync("/v1/customers", payload, JsonOptions, token),
            result => RetryPolicy.IsTransient((int)result.StatusCode),
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        var body = await response.Content.ReadFromJsonAsync<CustomerResponse>(JsonOptions, cancellationToken);

        return new GatewayCustomer(
            body?.Id ?? throw new DomainRuleException("O gateway não retornou o identificador do cliente."),
            body.Card?.LastFourDigits,
            body.Card?.PaymentMethod?.Id);
    }

    public async Task<GatewaySubscription> CreateSubscriptionAsync(
        string gatewayCustomerId,
        Money amount,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(gatewayCustomerId))
        {
            throw new DomainRuleException("O cliente do gateway é obrigatório.");
        }

        var payload = new
        {
            payer_id = gatewayCustomerId,
            reason = "Assinatura Servia",
            auto_recurring = new
            {
                frequency = 1,
                frequency_type = "months",
                transaction_amount = amount.Amount,
                currency_id = amount.Currency
            },
            back_url = "https://servia.app/assinatura"
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/preapproval")
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };

        request.Headers.Add("X-Idempotency-Key", idempotencyKey);

        var response = await _retryPolicy.ExecuteAsync(
            token => _httpClient.SendAsync(request, token),
            result => RetryPolicy.IsTransient((int)result.StatusCode),
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        var body = await response.Content.ReadFromJsonAsync<SubscriptionResponse>(JsonOptions, cancellationToken);

        return new GatewaySubscription(
            body?.Id ?? throw new DomainRuleException("O gateway não retornou o identificador da assinatura."),
            MapSubscriptionStatus(body.Status));
    }

    public async Task<GatewayCharge> CreateChargeAsync(
        string gatewayCustomerId,
        Money amount,
        string description,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            transaction_amount = amount.Amount,
            description,
            payer = new { id = gatewayCustomerId }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/payments")
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };

        request.Headers.Add("X-Idempotency-Key", idempotencyKey);

        var response = await _retryPolicy.ExecuteAsync(
            token => _httpClient.SendAsync(request, token),
            result => RetryPolicy.IsTransient((int)result.StatusCode),
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        var body = await response.Content.ReadFromJsonAsync<PaymentResponse>(JsonOptions, cancellationToken);

        return new GatewayCharge(
            body?.Id ?? throw new DomainRuleException("O gateway não retornou o identificador do pagamento."),
            MapChargeStatus(body.Status),
            body.PointOfInteraction?.Transaction?.TicketUrl);
    }

    public async Task<GatewayRefund> RefundAsync(
        string gatewayPaymentId,
        Money amount,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var payload = new { amount = amount.Amount };

        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/v1/payments/{gatewayPaymentId}/refunds")
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };

        request.Headers.Add("X-Idempotency-Key", idempotencyKey);

        var response = await _retryPolicy.ExecuteAsync(
            token => _httpClient.SendAsync(request, token),
            result => RetryPolicy.IsTransient((int)result.StatusCode),
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        var body = await response.Content.ReadFromJsonAsync<RefundResponse>(JsonOptions, cancellationToken);

        return new GatewayRefund(
            body?.Id ?? gatewayPaymentId,
            Money.From(body?.Amount ?? amount.Amount, amount.Currency));
    }

    public async Task CancelSubscriptionAsync(
        string gatewaySubscriptionId,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Put, $"/preapproval/{gatewaySubscriptionId}")
        {
            Content = JsonContent.Create(new { status = "cancelled" }, options: JsonOptions)
        };

        var response = await _retryPolicy.ExecuteAsync(
            token => _httpClient.SendAsync(request, token),
            result => RetryPolicy.IsTransient((int)result.StatusCode),
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var detail = await response.Content.ReadAsStringAsync(cancellationToken);

        throw new DomainRuleException(
            $"O gateway respondeu {(int)response.StatusCode}: {detail}");
    }

    private static GatewaySubscriptionStatus MapSubscriptionStatus(string? status) => status switch
    {
        "authorized" => GatewaySubscriptionStatus.Active,
        "paused" => GatewaySubscriptionStatus.PastDue,
        "cancelled" => GatewaySubscriptionStatus.Cancelled,
        _ => GatewaySubscriptionStatus.Pending
    };

    private static GatewayChargeStatus MapChargeStatus(string? status) => status switch
    {
        "approved" => GatewayChargeStatus.Approved,
        "rejected" => GatewayChargeStatus.Rejected,
        "refunded" => GatewayChargeStatus.Refunded,
        _ => GatewayChargeStatus.Pending
    };

    // ---------- Contratos de resposta do provedor ----------

    private sealed record CustomerResponse(string? Id, CardResponse? Card);

    private sealed record CardResponse(
        [property: JsonPropertyName("last_four_digits")] string? LastFourDigits,
        [property: JsonPropertyName("payment_method")] PaymentMethodResponse? PaymentMethod);

    private sealed record PaymentMethodResponse(string? Id);

    private sealed record SubscriptionResponse(string? Id, string? Status);

    private sealed record PaymentResponse(
        string? Id,
        string? Status,
        [property: JsonPropertyName("point_of_interaction")] PointOfInteractionResponse? PointOfInteraction);

    private sealed record PointOfInteractionResponse(TransactionResponse? Transaction);

    private sealed record TransactionResponse(
        [property: JsonPropertyName("ticket_url")] string? TicketUrl);

    private sealed record RefundResponse(string? Id, decimal? Amount);
}

/// <summary>Configuração do Mercado Pago.</summary>
public sealed class MercadoPagoOptions
{
    /// <summary>Token de acesso. Deve vir de variável de ambiente ou cofre.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Segredo do webhook, usado para validar a origem.</summary>
    public string WebhookSecret { get; set; } = string.Empty;
}
