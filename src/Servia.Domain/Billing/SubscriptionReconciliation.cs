namespace Servia.Domain.Billing;

public sealed class SubscriptionReconciliation
{
    public Guid SubscriptionId { get; init; }
    public DateTimeOffset NextCheckAt { get; set; }
    public DateTimeOffset? LastCheckedAt { get; set; }
    public DateTimeOffset? RateLimitedUntil { get; set; }
    public Guid? LeaseId { get; set; }
    public int Attempts { get; set; }
    public bool NeedsIntervention { get; set; }
    public string? Result { get; set; }
}
