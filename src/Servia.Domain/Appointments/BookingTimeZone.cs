namespace Servia.Domain.Appointments;

/// <summary>Fuso operacional dos horários locais da agenda.</summary>
public static class BookingTimeZone
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public static bool TryGetUtcInstant(DateOnly date, TimeOnly time, out DateTimeOffset instant)
    {
        var local = date.ToDateTime(time);
        if (Zone.IsInvalidTime(local))
        {
            instant = default;
            return false;
        }

        instant = TimeZoneInfo.ConvertTimeToUtc(local, Zone);
        return true;
    }

    public static DateTimeOffset GetUtcStartOfDay(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue);
        // Em datas históricas de início do horário de verão, a meia-noite pode não existir.
        while (Zone.IsInvalidTime(local))
        {
            local = local.AddMinutes(1);
        }

        return TimeZoneInfo.ConvertTimeToUtc(local, Zone);
    }
}
