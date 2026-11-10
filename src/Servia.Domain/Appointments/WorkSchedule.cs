using Servia.Domain.Common;

namespace Servia.Domain.Appointments;

/// <summary>
/// Jornada semanal de um profissional.
/// Protege as invariantes de jornada: ao menos um dia de trabalho,
/// início estritamente anterior ao fim e granularidade de horário válida.
/// </summary>
public sealed class WorkSchedule
{
    public const int MinSlotIntervalMinutes = 5;
    public const int MaxSlotIntervalMinutes = 120;

    private readonly HashSet<DayOfWeek> _workDays = [];

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>,
    /// que é o único caminho público de construção.
    /// </summary>
    private WorkSchedule()
    {
    }

    private WorkSchedule(
        IEnumerable<DayOfWeek> workDays,
        TimeOnly startTime,
        TimeOnly endTime,
        int slotIntervalMinutes)
    {
        _workDays = [.. workDays];
        StartTime = startTime;
        EndTime = endTime;
        SlotIntervalMinutes = slotIntervalMinutes;
    }

    public IReadOnlySet<DayOfWeek> WorkDays => _workDays;

    public TimeOnly StartTime { get; private set; }

    public TimeOnly EndTime { get; private set; }

    public int SlotIntervalMinutes { get; private set; }

    /// <summary>Intervalo de expediente como objeto de valor comparável.</summary>
    public TimeRange WorkingHours => TimeRange.Between(StartTime, EndTime);

    public static WorkSchedule Create(
        IEnumerable<DayOfWeek> workDays,
        TimeOnly startTime,
        TimeOnly endTime,
        int slotIntervalMinutes = 30)
    {
        ArgumentNullException.ThrowIfNull(workDays);

        var days = workDays.Distinct().ToArray();

        if (days.Length == 0)
        {
            throw new DomainRuleException("A jornada deve possuir ao menos um dia de trabalho.");
        }

        if (startTime >= endTime)
        {
            throw new DomainRuleException("O início do expediente deve ser anterior ao fim.");
        }

        if (slotIntervalMinutes is < MinSlotIntervalMinutes or > MaxSlotIntervalMinutes)
        {
            throw new DomainRuleException(
                $"A granularidade de horários deve estar entre {MinSlotIntervalMinutes} e {MaxSlotIntervalMinutes} minutos.");
        }

        return new WorkSchedule(days, startTime, endTime, slotIntervalMinutes);
    }

    /// <summary>Indica se o profissional trabalha na data informada.</summary>
    public bool WorksOn(DateOnly date) => _workDays.Contains(date.DayOfWeek);

    /// <summary>
    /// Indica se um atendimento de determinada duração cabe integralmente
    /// dentro do expediente, começando no horário informado.
    /// </summary>
    public bool FitsWithinSchedule(TimeOnly time, int durationMinutes)
    {
        if (durationMinutes <= 0)
        {
            return false;
        }

        var appointment = TimeRange.From(time, durationMinutes);
        return appointment.IsContainedIn(WorkingHours);
    }

    /// <summary>
    /// Gera os horários candidatos do dia, respeitando a granularidade
    /// configurada e a duração do serviço.
    /// </summary>
    public IEnumerable<TimeOnly> GenerateSlots(int durationMinutes)
    {
        if (durationMinutes <= 0)
        {
            yield break;
        }

        var workingHours = WorkingHours;
        var cursor = workingHours.StartMinutes;

        while (cursor + durationMinutes <= workingHours.EndMinutes)
        {
            yield return TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(cursor));
            cursor += SlotIntervalMinutes;
        }
    }
}
