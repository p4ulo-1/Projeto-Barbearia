using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Servia.Domain.Billing;
using Servia.Domain.Common;

namespace Servia.Infrastructure.Integrations;

public sealed partial class MercadoPagoGateway
{
    private static readonly JsonSerializerOptions CheckoutJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public async Task<RecurringAuthorization> CreateAsync(RecurringCheckoutRequest checkout, CancellationToken cancellationToken)
    {
        var returnUrl = new UriBuilder(_returnUrl) { Query = $"checkoutKey={checkout.CheckoutKey:D}" };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/preapproval")
        {
            Content = JsonContent.Create(new
            {
                reason = "Assinatura Servia",
                payer_email = checkout.PayerEmail,
                external_reference = checkout.ExternalReference,
                back_url = returnUrl.Uri.AbsoluteUri,
                status = "pending",
                auto_recurring = new
                {
                    frequency = checkout.IntervalMonths,
                    frequency_type = "months",
                    transaction_amount = checkout.Amount.Amount,
                    currency_id = checkout.Amount.Currency
                }
            }, options: CheckoutJson)
        };

        var response = await SendCheckoutAsync<Preapproval>(request, cancellationToken);
        return ToAuthorization(response);
    }

    public async Task<RecurringAuthorization> FindAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/preapproval/{Uri.EscapeDataString(subscriptionId)}");
        return ToAuthorization(await SendCheckoutAsync<Preapproval>(request, cancellationToken));
    }

    public async Task<ApprovedRecurringPayment?> FindFirstApprovedPaymentAsync(
        string subscriptionId, Money amount, CancellationToken cancellationToken)
    {
        using var search = new HttpRequestMessage(HttpMethod.Get,
            $"/authorized_payments/search?preapproval_id={Uri.EscapeDataString(subscriptionId)}&offset=0&limit=20");
        var list = await SendCheckoutAsync<InvoiceSearch>(search, cancellationToken);
        // Sem a primeira parcela identificável, uma cobrança posterior não serve como prova.
        if (list.Paging is not { Offset: 0, Total: > 0 } paging || list.Results is not { Length: > 0 } invoices
            || paging.Total != invoices.Length
            || invoices.Any(invoice => invoice.PreapprovalId != subscriptionId || !invoice.DebitDate.HasValue))
        {
            return null;
        }

        var earliest = invoices.Min(invoice => invoice.DebitDate);
        var first = invoices.Where(invoice => invoice.DebitDate == earliest).ToArray();
        if (first.Length != 1 || first[0] is not { Type: "scheduled", Payment: { Id: > 0, Status: "approved" } linked }
            || first[0].TransactionAmount != amount.Amount || first[0].CurrencyId != amount.Currency)
        {
            return null;
        }

        var id = linked.Id.ToString(CultureInfo.InvariantCulture);
        using var get = new HttpRequestMessage(HttpMethod.Get, $"/v1/payments/{id}");
        var payment = await SendCheckoutAsync<RecurringPayment>(get, cancellationToken);
        return payment.Id == linked.Id && payment.Status == "approved"
            && payment.TransactionAmount == amount.Amount && payment.CurrencyId == amount.Currency
            && payment.TransactionAmountRefunded == 0m && payment.DateApproved is { } approved
            ? new ApprovedRecurringPayment(id, first[0].DebitDate!.Value, approved)
            : null;
    }

    private async Task<T> SendCheckoutAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            using var response = await _httpClient.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                // 400 documenta recusa da requisição de criação. Falhas de transporte,
                // timeouts, conflitos e outros resultados não liberam uma nova criação.
                throw new RecurringCheckoutGatewayException(
                    request.Method == HttpMethod.Post && response.StatusCode == HttpStatusCode.BadRequest);
            }
            return await response.Content.ReadFromJsonAsync<T>(CheckoutJson, timeout.Token)
                ?? throw new RecurringCheckoutGatewayException();
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException
            or NotSupportedException or IOException
            || exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new RecurringCheckoutGatewayException();
        }
    }

    private static RecurringAuthorization ToAuthorization(Preapproval item)
    {
        var id = item.Id is { Length: > 0 and <= 100 } candidate
            && candidate.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-')
            ? candidate
            : null;
        var url = id is not null && item.InitPoint is { Length: <= 2048 } link
            && Uri.TryCreate(link, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            && uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Fragment)
            && uri.Host is "www.mercadopago.com.br" or "mercadopago.com.br"
            && uri.AbsolutePath == "/subscriptions/checkout" && uri.Query == $"?preapproval_id={id}"
            ? link
            : null;
        return new RecurringAuthorization(id, item.ExternalReference, item.Status,
            item.AutoRecurring?.TransactionAmount, item.AutoRecurring?.CurrencyId,
            item.AutoRecurring?.Frequency, item.AutoRecurring?.FrequencyType, url);
    }

    private sealed record Preapproval(string? Id, string? ExternalReference, string? Status,
        string? InitPoint, Recurrence? AutoRecurring);
    private sealed record Recurrence(decimal? TransactionAmount, string? CurrencyId, int? Frequency, string? FrequencyType);
    private sealed record InvoiceSearch(InvoicePaging? Paging, RecurringInvoice[]? Results);
    private sealed record InvoicePaging(int? Offset, int? Total);
    private sealed record RecurringInvoice(string? PreapprovalId, string? Type, DateTimeOffset? DebitDate,
        decimal? TransactionAmount, string? CurrencyId, InvoicePayment? Payment);
    private sealed record InvoicePayment(long Id, string? Status);
    private sealed record RecurringPayment(long Id, string? Status, decimal? TransactionAmount,
        string? CurrencyId, decimal? TransactionAmountRefunded, DateTimeOffset? DateApproved);
}
