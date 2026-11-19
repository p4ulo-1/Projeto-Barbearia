using Servia.Domain.Common;

namespace Servia.Domain.Billing;

public static class DelinquencyPolicy
{
    public static TimeSpan GracePeriod { get; } = TimeSpan.FromHours(48);

    public static DelinquencyState Evaluate(DateTimeOffset failedAt, DateTimeOffset now)
    {
        if (now < failedAt)
        {
            throw new DomainRuleException("O instante atual não pode ser anterior à falha.");
        }
        return now < WriteBlockStartsAt(failedAt) ? DelinquencyState.Warning : DelinquencyState.WriteBlocked;
    }

    public static bool CanWrite(DelinquencyState state) => state == DelinquencyState.Warning;

    public static DateTimeOffset WriteBlockStartsAt(DateTimeOffset failedAt) => failedAt + GracePeriod;
}

public enum DelinquencyState
{
    Warning = 1,
    WriteBlocked = 2
}
