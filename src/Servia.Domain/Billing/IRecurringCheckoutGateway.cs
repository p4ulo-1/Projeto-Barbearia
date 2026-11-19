using Servia.Domain.Common;

namespace Servia.Domain.Billing;

public interface IRecurringCheckoutGateway
{
    bool CanStartCheckout { get; }
    Task<string> VerifyTrialPlanAsync(string planCode, Money amount, CancellationToken cancellationToken);
    Task<RecurringAuthorization> CreateAsync(RecurringCheckoutRequest request, CancellationToken cancellationToken);
    Task<RecurringAuthorization> FindAsync(string subscriptionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> SearchByCorrelationAsync(string? payerEmail, string? trialPlanId,
        string externalReference, CancellationToken cancellationToken);
    Task<RecurringCycleEvidence> FindFirstPaymentAsync(string subscriptionId, Money amount,
        CancellationToken cancellationToken);
    Task<RecurringCycleEvidence> FindCyclePaymentAsync(string subscriptionId, DateTimeOffset dueAt,
        Money amount, CancellationToken cancellationToken);
    Task RequestCancellationAsync(string subscriptionId, CancellationToken cancellationToken);
    Task<RecurringNotificationPayment> ReadNotificationPaymentAsync(string resourceId, bool isInvoice,
        CancellationToken cancellationToken);
}

public sealed record RecurringCheckoutRequest(Guid CheckoutKey, string ExternalReference, string PayerEmail,
    Money Amount, int IntervalMonths, string? TrialPlanId = null, string? CardToken = null);

public sealed record RecurringAuthorization(string? Id, string? ExternalReference, string? Status,
    decimal? Amount, string? Currency, int? Frequency, string? FrequencyType, string? CheckoutUrl,
    string? TrialPlanId = null, DateTimeOffset? CreatedAt = null, DateTimeOffset? NextChargeAt = null);

public sealed record ApprovedRecurringPayment(string Id, DateTimeOffset DueAt, DateTimeOffset ApprovedAt);
public sealed record RecurringCycleEvidence(ApprovedRecurringPayment? Payment = null, bool Rejected = false,
    DateTimeOffset? DueAt = null);
public sealed record RecurringNotificationPayment(string? SubscriptionId, DateTimeOffset? DueAt, string? PaymentStatus);

/// <summary>Erro seguro para a aplicação, sem corpo ou credenciais do provedor.</summary>
public sealed class RecurringCheckoutGatewayException(bool rejected = false, bool rateLimited = false,
    TimeSpan? retryAfter = null)
    : Exception("Não foi possível confirmar o resultado da operação no provedor de pagamento.")
{
    public bool Rejected { get; } = rejected;
    public bool RateLimited { get; } = rateLimited;
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
