using Servia.Domain.Appointments;

namespace Servia.Domain.Policies;

/// <summary>
/// Política padrão: o atendimento precisa caber integralmente na jornada,
/// respeitando o dia de trabalho e o intervalo de expediente.
/// </summary>
public sealed class StandardAvailabilityPolicy : IAvailabilityPolicy
{
    public bool IsAvailable(WorkSchedule schedule, DateOnly date, TimeOnly time, int durationMinutes)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        return schedule.WorksOn(date)
            && schedule.FitsWithinSchedule(time, durationMinutes);
    }

    public string Describe() => "Jornada padrão";
}
