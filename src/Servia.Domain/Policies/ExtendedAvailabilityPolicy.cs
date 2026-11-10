using Servia.Domain.Appointments;
using Servia.Domain.Common;

namespace Servia.Domain.Policies;

/// <summary>
/// Política estendida: além da jornada padrão, aceita atendimentos que
/// terminam até <see cref="ToleranceMinutes"/> minutos após o fim do expediente.
///
/// Usada por profissionais que trabalham até o último minuto do expediente
/// e toleram pequenos estouros de duração.
/// </summary>
public sealed class ExtendedAvailabilityPolicy : IAvailabilityPolicy
{
    public const int ToleranceMinutes = 5;

    public bool IsAvailable(WorkSchedule schedule, DateOnly date, TimeOnly time, int durationMinutes)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        if (!schedule.WorksOn(date))
        {
            return false;
        }

        if (schedule.FitsWithinSchedule(time, durationMinutes))
        {
            return true;
        }

        return EndsWithinTolerance(schedule, time, durationMinutes);
    }

    private static bool EndsWithinTolerance(WorkSchedule schedule, TimeOnly time, int durationMinutes)
    {
        if (durationMinutes <= 0)
        {
            return false;
        }

        var appointment = TimeRange.From(time, durationMinutes);
        var workingHours = schedule.WorkingHours;

        var startsInside = appointment.StartMinutes >= workingHours.StartMinutes;
        var endsWithinTolerance = appointment.EndMinutes <= workingHours.EndMinutes + ToleranceMinutes;

        return startsInside && endsWithinTolerance;
    }

    public string Describe() => $"Jornada estendida (tolerância de {ToleranceMinutes} minutos)";
}
