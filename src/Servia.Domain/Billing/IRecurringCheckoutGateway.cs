using Servia.Domain.Common;

namespace Servia.Domain.Billing;

public interface IRecurringCheckoutGateway
{
    Task<RecurringAuthorization> CreateAsync(RecurringCheckoutRequest request, CancellationToken cancellationToken);
    Task<RecurringAuthorization> FindAsync(string subscriptionId, CancellationToken cancellationToken);
    Task<ApprovedRecurringPayment?> FindFirstApprovedPaymentAsync(string subscriptionId, Money amount,
        CancellationToken cancellationToken);
}

public sealed record RecurringCheckoutRequest(Guid CheckoutKey, string ExternalReference, string PayerEmail,
    Money Amount, int IntervalMonths);

public sealed record RecurringAuthorization(string? Id, string? ExternalReference, string? Status,
    decimal? Amount, string? Currency, int? Frequency, string? FrequencyType, string? CheckoutUrl);

public sealed record ApprovedRecurringPayment(string Id, DateTimeOffset DueAt, DateTimeOffset ApprovedAt);

/// <summary>Erro seguro para a aplicação, sem corpo ou credenciais do provedor.</summary>
public sealed class RecurringCheckoutGatewayException(bool rejected = false)
    : Exception("Não foi possível confirmar o resultado da operação no provedor de pagamento.")
{
    public bool Rejected { get; } = rejected;
}
