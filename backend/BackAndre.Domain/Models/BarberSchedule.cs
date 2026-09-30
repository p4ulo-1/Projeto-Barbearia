namespace BackAndre.Domain.Models;

public class BarberSchedule
{
    private BarberSchedule()
    {
    }

    public BarberSchedule(IEnumerable<int> workDays, TimeOnly startTime, TimeOnly endTime)
    {
        var normalizedDays = workDays.Distinct().Order().ToArray();
        if (normalizedDays.Length == 0 || normalizedDays.Any(day => day is < 0 or > 6))
        {
            throw new DomainRuleException("A jornada deve possuir ao menos um dia válido entre 0 e 6.");
        }

        if (startTime >= endTime)
        {
            throw new DomainRuleException("O início do expediente deve ser anterior ao fim.");
        }

        WorkDays = string.Join(',', normalizedDays);
        StartTime = startTime;
        EndTime = endTime;
    }

    public string WorkDays { get; private set; } = string.Empty;
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }

    public IReadOnlyCollection<int> WorkDayNumbers => WorkDays
        .Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(int.Parse)
        .ToArray();

    public bool WorksOn(DateOnly date) => WorkDayNumbers.Contains((int)date.DayOfWeek);

    public bool FitsWithinSchedule(TimeOnly time, int durationMinutes)
    {
        if (durationMinutes <= 0)
        {
            return false;
        }

        var scheduleStart = StartTime.Hour * 60 + StartTime.Minute;
        var scheduleEnd = EndTime.Hour * 60 + EndTime.Minute;
        var appointmentStart = time.Hour * 60 + time.Minute;
        var appointmentEnd = appointmentStart + durationMinutes;

        return appointmentStart >= scheduleStart && appointmentEnd <= scheduleEnd;
    }
}
