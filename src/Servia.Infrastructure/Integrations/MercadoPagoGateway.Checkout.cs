using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Servia.Domain.Billing;
using Servia.Domain.Common;

namespace Servia.Infrastructure.Integrations;

public sealed partial class MercadoPagoGateway
{
    private static readonly JsonSerializerOptions CheckoutJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<string> VerifyTrialPlanAsync(string planCode, Money amount, CancellationToken cancellationToken)
    {
        if (!_trialPlanIds.TryGetValue(planCode, out var id) || string.IsNullOrWhiteSpace(id))
        {
            _logger.LogWarning("Mercado Pago: plano de trial sem associação local.");
            throw new RecurringCheckoutGatewayException();
        }
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/preapproval_plan/{Uri.EscapeDataString(id)}");
        var plan = await SendCheckoutAsync<TrialPlan>(request, cancellationToken);
        if (plan.Id != id || plan.Status != "active"
            || plan.PaymentMethodsAllowed?.PaymentTypes is not [{ Id: "credit_card" }]
            || plan.AutoRecurring is not { Frequency: 1, FrequencyType: "months",
                FreeTrial: { Frequency: 14, FrequencyType: "days" }, BillingDay: null or 0,
                BillingDayProportional: null or false, Repetitions: null or 0 } recurring
            || recurring.TransactionAmount != amount.Amount || recurring.CurrencyId != amount.Currency)
        {
            _logger.LogWarning("Mercado Pago: plano remoto não atende aos requisitos do trial.");
            throw new RecurringCheckoutGatewayException();
        }
        return id;
    }

    public async Task<RecurringAuthorization> CreateAsync(RecurringCheckoutRequest checkout, CancellationToken cancellationToken)
    {
        if (!CanStartCheckout || string.IsNullOrWhiteSpace(checkout.TrialPlanId)
            || string.IsNullOrWhiteSpace(checkout.CardToken)) throw new RecurringCheckoutGatewayException();
        var returnUrl = new UriBuilder(_returnUrl) { Query = $"checkoutKey={checkout.CheckoutKey:D}" };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/preapproval")
        {
            Content = JsonContent.Create(new
            {
                reason = "Assinatura Servia",
                payer_email = checkout.PayerEmail,
                external_reference = checkout.ExternalReference,
                back_url = returnUrl.Uri.AbsoluteUri,
                status = "authorized",
                preapproval_plan_id = checkout.TrialPlanId,
                card_token_id = checkout.CardToken
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

    public async Task<IReadOnlyList<string>> SearchByCorrelationAsync(string? payerEmail, string? trialPlanId,
        string externalReference, CancellationToken cancellationToken)
    {
        var filter = payerEmail is not null ? $"payer_email={Uri.EscapeDataString(payerEmail)}"
            : trialPlanId is not null ? $"preapproval_plan_id={Uri.EscapeDataString(trialPlanId)}"
            : throw new ArgumentException("A busca exige um filtro de assinatura.");
        const int pageSize = 20;
        const int maxResults = 100;
        var matches = new List<string>();
        int? total = null;
        for (var offset = 0; offset < maxResults; offset += pageSize)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"/preapproval/search?{filter}&offset={offset}&limit={pageSize}");
            var page = await SendCheckoutAsync<PreapprovalSearch>(request, cancellationToken);
            if (page.Paging is not { Total: >= 0 and <= maxResults } paging || paging.Offset != offset
                || (total.HasValue && total != paging.Total) || page.Results is null
                || page.Results.Length != Math.Min(pageSize, paging.Total.Value - offset))
            {
                throw new RecurringCheckoutGatewayException();
            }
            total = paging.Total;
            foreach (var entry in page.Results)
            {
                if (entry.ExternalReference.ValueKind != JsonValueKind.String
                    || entry.ExternalReference.GetString() != externalReference) continue;
                if (!IsExternalId(entry.Id)) throw new RecurringCheckoutGatewayException();
                matches.Add(entry.Id!);
            }
            if (offset + pageSize >= total) return matches;
        }
        throw new RecurringCheckoutGatewayException();
    }

    public Task<RecurringCycleEvidence> FindCyclePaymentAsync(string subscriptionId, DateTimeOffset dueAt,
        Money amount, CancellationToken cancellationToken) => FindPaymentAsync(subscriptionId, dueAt, amount, cancellationToken);

    public async Task RequestCancellationAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/preapproval/{Uri.EscapeDataString(subscriptionId)}")
        {
            Content = JsonContent.Create(new { status = "canceled" })
        };
        await SendCheckoutAsync<Preapproval>(request, cancellationToken);
    }

    public async Task UpdateRecurringPriceAsync(string subscriptionId, Money amount, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/preapproval/{Uri.EscapeDataString(subscriptionId)}")
        {
            Content = JsonContent.Create(new { auto_recurring = new { transaction_amount = amount.Amount, currency_id = amount.Currency } })
        };
        await SendCheckoutAsync<Preapproval>(request, cancellationToken);
    }

    public async Task<bool> IsCreditCardMethodAsync(string method, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/payment_methods");
        var methods = await SendCheckoutAsync<PaymentMethod[]>(request, cancellationToken);
        return methods.Any(item => item.Id == method && item.PaymentTypeId == "credit_card" && item.Status == "active");
    }

    public async Task CreateUpgradePaymentAsync(Guid operationId, Money amount, string token, string method,
        string email, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/payments")
        {
            Content = JsonContent.Create(new { transaction_amount = amount.Amount, token, installments = 1,
                payment_method_id = method, payer = new { email }, external_reference = UpgradeReference(operationId),
                description = "Diferença proporcional de plano Servia" })
        };
        request.Headers.Add("X-Idempotency-Key", operationId.ToString("D"));
        await SendCheckoutAsync<RecurringPayment>(request, cancellationToken);
    }

    public async Task<UpgradePaymentEvidence?> FindUpgradePaymentAsync(Guid operationId, CancellationToken cancellationToken)
    {
        var reference = UpgradeReference(operationId);
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/v1/payments/search?external_reference={Uri.EscapeDataString(reference)}&limit=2");
        var result = await SendCheckoutAsync<PaymentSearch>(request, cancellationToken);
        if (result is not { Paging.Total: 1, Results: [{ Id: > 0 } match] }) return null;
        var payment = await ReadPaymentAsync(match.Id, cancellationToken);
        return payment.ExternalReference == reference ? new(payment.Id.ToString(CultureInfo.InvariantCulture),
            payment.Status, payment.TransactionAmount, payment.CurrencyId, payment.PaymentTypeId,
            payment.TransactionAmountRefunded, payment.DateApproved) : null;
    }

    private static string UpgradeReference(Guid operationId) => $"servia-upgrade:{operationId:N}";

    public async Task<RecurringNotificationPayment> ReadNotificationPaymentAsync(string resourceId, bool isInvoice,
        CancellationToken cancellationToken)
    {
        if (!long.TryParse(resourceId, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)
        {
            throw new RecurringCheckoutGatewayException();
        }

        if (isInvoice)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/authorized_payments/{id}");
            var invoice = await SendCheckoutAsync<RecurringInvoice>(request, cancellationToken);
            if (invoice.Id != id || invoice.Type != "scheduled" || invoice.DebitDate is null
                || string.IsNullOrWhiteSpace(invoice.PreapprovalId))
            {
                throw new RecurringCheckoutGatewayException();
            }
            var linked = invoice.Payment is { Id: > 0 } payment
                ? await ReadPaymentAsync(payment.Id, cancellationToken) : null;
            return new(invoice.PreapprovalId, invoice.DebitDate, PaymentStatus(linked));
        }

        var notified = await ReadPaymentAsync(id, cancellationToken);
        using var search = new HttpRequestMessage(HttpMethod.Get, $"/authorized_payments/search?payment_id={id}&offset=0&limit=2");
        var result = await SendCheckoutAsync<InvoiceSearch>(search, cancellationToken);
        if (result is { Paging: { Offset: 0, Total: 0 }, Results.Length: 0 })
        {
            return new(null, null, PaymentStatus(notified), notified.ExternalReference);
        }
        if (result is not { Paging: { Offset: 0, Total: 1 }, Results: [var match] }
            || match is not { Type: "scheduled", Payment.Id: var paymentId, DebitDate: not null }
            || paymentId != notified.Id || string.IsNullOrWhiteSpace(match.PreapprovalId))
        {
            throw new RecurringCheckoutGatewayException();
        }
        return new(match.PreapprovalId, match.DebitDate, PaymentStatus(notified));
    }

    private async Task<RecurringPayment> ReadPaymentAsync(long id, CancellationToken cancellationToken)
    {
        using var get = new HttpRequestMessage(HttpMethod.Get, $"/v1/payments/{id}");
        var payment = await SendCheckoutAsync<RecurringPayment>(get, cancellationToken);
        return payment.Id == id ? payment : throw new RecurringCheckoutGatewayException();
    }

    private static string? PaymentStatus(RecurringPayment? payment) => payment is { Status: "approved" }
        && (payment.TransactionAmountRefunded != 0m || !payment.DateApproved.HasValue) ? null : payment?.Status;

    private async Task<RecurringCycleEvidence> FindPaymentAsync(
        string subscriptionId, DateTimeOffset? dueAt, Money amount, CancellationToken cancellationToken)
    {
        var invoices = new List<RecurringInvoice>();
        int? total = null;
        for (var page = 0; page < 10; page++)
        {
            using var search = new HttpRequestMessage(HttpMethod.Get,
                $"/authorized_payments/search?preapproval_id={Uri.EscapeDataString(subscriptionId)}&offset={invoices.Count}&limit=20");
            var list = await SendCheckoutAsync<InvoiceSearch>(search, cancellationToken);
            if (total is null && list is { Paging: { Offset: 0, Total: 0 }, Results.Length: 0 })
            {
                return new(NoInvoice: true);
            }
            if (list.Paging is not { Total: > 0 } paging || paging.Offset != invoices.Count
                || (total.HasValue && total != paging.Total) || list.Results is not { Length: > 0 } items
                || items.Any(invoice => invoice.PreapprovalId != subscriptionId || !invoice.DebitDate.HasValue))
            {
                return new();
            }
            total = paging.Total;
            invoices.AddRange(items);
            if (invoices.Count >= total)
            {
                break;
            }
        }
        if (invoices.Count == 0 || invoices.Count != total) return new();

        var earliest = dueAt ?? invoices.Min(invoice => invoice.DebitDate);
        var first = invoices.Where(invoice => invoice.DebitDate == earliest).ToArray();
        if (first.Length == 0)
        {
            return new(NoInvoice: true);
        }
        if (first.Length != 1 || first[0] is not { Type: "scheduled", DebitDate: { } debit })
        {
            return new();
        }
        if (first[0].TransactionAmount != amount.Amount || first[0].CurrencyId != amount.Currency)
            return new(InvoiceAmount: first[0].TransactionAmount, Currency: first[0].CurrencyId);
        if (first[0].Payment is not { Id: > 0 } linked)
        {
            return new(DueAt: debit, InvoiceAmount: amount.Amount, Currency: amount.Currency);
        }

        var id = linked.Id.ToString(CultureInfo.InvariantCulture);
        var payment = await ReadPaymentAsync(linked.Id, cancellationToken);
        if (payment.TransactionAmount != amount.Amount || payment.CurrencyId != amount.Currency)
        {
            return new(InvoiceAmount: amount.Amount, Currency: amount.Currency);
        }
        if (payment.Status == "rejected")
        {
            return new(Rejected: true, DueAt: debit, InvoiceAmount: amount.Amount, Currency: amount.Currency);
        }
        return linked.Status == "approved" && payment.Status == "approved"
            && payment.TransactionAmountRefunded == 0m && payment.DateApproved is { } approved
            ? new(new ApprovedRecurringPayment(id, debit, approved), DueAt: debit, InvoiceAmount: amount.Amount, Currency: amount.Currency)
            : new(DueAt: debit, InvoiceAmount: amount.Amount, Currency: amount.Currency);
    }

    private async Task<T> SendCheckoutAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken,
        [CallerMemberName] string operation = "")
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Mercado Pago: operação {Operation} retornou HTTP {StatusCode}.",
                    operation, (int)response.StatusCode);
                // 400 documenta recusa da requisição de criação. Falhas de transporte,
                // timeouts, conflitos e outros resultados não liberam uma nova criação.
                var retry = response.Headers.RetryAfter;
                throw new RecurringCheckoutGatewayException(
                    request.Method == HttpMethod.Post && response.StatusCode == HttpStatusCode.BadRequest,
                    response.StatusCode == HttpStatusCode.TooManyRequests,
                    retry?.Delta ?? (retry?.Date - DateTimeOffset.UtcNow));
            }
            // JSON de API usa UTF-8. O corpo e os cabeçalhos da resposta não entram no diagnóstico.
            using var body = await response.Content.ReadAsStreamAsync(timeout.Token);
            return await JsonSerializer.DeserializeAsync<T>(body, CheckoutJson, timeout.Token)
                ?? throw new JsonException();
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException
            or NotSupportedException or IOException
            || exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Mercado Pago: operação {Operation} sem resposta utilizável ({FailureType}).",
                operation, exception.GetType().Name);
            throw new RecurringCheckoutGatewayException();
        }
    }

    private static RecurringAuthorization ToAuthorization(Preapproval item)
    {
        var id = IsExternalId(item.Id) ? item.Id : null;
        return new RecurringAuthorization(id, item.ExternalReference, item.Status,
            item.AutoRecurring?.TransactionAmount, item.AutoRecurring?.CurrencyId,
            item.AutoRecurring?.Frequency, item.AutoRecurring?.FrequencyType,
            item.PreapprovalPlanId, AtDatabasePrecision(item.DateCreated), AtDatabasePrecision(item.NextPaymentDate));
    }

    private static DateTimeOffset? AtDatabasePrecision(DateTimeOffset? instant) => instant is { UtcTicks: var ticks }
        ? new DateTimeOffset(ticks - ticks % 10, TimeSpan.Zero) : null;

    internal static bool IsExternalId(string? id) => id is { Length: > 0 and <= 100 }
        && id.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');

    private sealed record PreapprovalSearch(InvoicePaging? Paging, PreapprovalSearchItem[]? Results);
    private sealed record PreapprovalSearchItem(string? Id, JsonElement ExternalReference);
    private sealed record Preapproval(string? Id, string? ExternalReference, string? Status,
        Recurrence? AutoRecurring, string? PreapprovalPlanId,
        DateTimeOffset? DateCreated, DateTimeOffset? NextPaymentDate);
    private sealed record TrialPlan(string? Id, string? Status, Recurrence? AutoRecurring,
        AllowedPaymentMethods? PaymentMethodsAllowed);
    private sealed record AllowedPaymentMethods(PaymentType[]? PaymentTypes);
    private sealed record PaymentType(string? Id);
    private sealed record FreeTrial(int? Frequency, string? FrequencyType);
    private sealed record Recurrence(decimal? TransactionAmount, string? CurrencyId, int? Frequency,
        string? FrequencyType, FreeTrial? FreeTrial, int? BillingDay, bool? BillingDayProportional, int? Repetitions);
    private sealed record InvoiceSearch(InvoicePaging? Paging, RecurringInvoice[]? Results);
    private sealed record InvoicePaging(int? Offset, int? Total);
    private sealed record RecurringInvoice(long? Id, string? PreapprovalId, string? Type, DateTimeOffset? DebitDate,
        decimal? TransactionAmount, string? CurrencyId, InvoicePayment? Payment);
    private sealed record InvoicePayment(long Id, string? Status);
    private sealed record RecurringPayment(long Id, string? Status, decimal? TransactionAmount,
        string? CurrencyId, decimal? TransactionAmountRefunded, DateTimeOffset? DateApproved,
        string? ExternalReference, string? PaymentTypeId);
    private sealed record PaymentMethod(string? Id, string? PaymentTypeId, string? Status);
    private sealed record PaymentSearch(InvoicePaging? Paging, RecurringPayment[]? Results);
}
