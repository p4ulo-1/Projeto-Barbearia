namespace Servia.Domain.Operations;

public sealed class MercadoPagoNotification
{
    public const string Subscription = "subscription_preapproval";
    public const string AuthorizedPayment = "subscription_authorized_payment";
    public const string Payment = "payment";

    public Guid Id { get; init; }
    public string ExternalId { get; init; } = null!;
    public string Topic { get; init; } = null!;
    public string ResourceId { get; init; } = null!;
    public string RequestId { get; init; } = null!;
    public long SignatureTimestamp { get; init; }
    public DateTimeOffset ReceivedAt { get; init; }
    public DateTimeOffset AvailableAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public Guid? LeaseId { get; set; }
    public int Attempts { get; set; }
    public string? Result { get; set; }

    public static bool IsSupported(string topic) => topic is Subscription or AuthorizedPayment or Payment;
}
