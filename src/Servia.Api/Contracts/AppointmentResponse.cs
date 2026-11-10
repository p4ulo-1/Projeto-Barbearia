using Servia.Domain.Appointments;

namespace Servia.Api.Contracts;

/// <summary>
/// Representação de um agendamento devolvida pela API.
///
/// Expõe apenas o que o cliente precisa saber. Não vaza detalhes internos
/// do agregado nem permite reconstruir o objeto de domínio.
/// </summary>
public sealed record AppointmentResponse(
    Guid Id,
    Guid TenantId,
    Guid CustomerId,
    Guid ServiceId,
    Guid ProfessionalId,
    DateOnly Date,
    TimeOnly Time,
    int DurationMinutes,
    string Status,
    string? CancellationReason,
    DateTimeOffset CreatedAt)
{
    public static AppointmentResponse From(Appointment appointment)
    {
        ArgumentNullException.ThrowIfNull(appointment);

        return new AppointmentResponse(
            appointment.Id,
            appointment.TenantId,
            appointment.CustomerId,
            appointment.ServiceId,
            appointment.ProfessionalId,
            appointment.Date,
            appointment.Time,
            appointment.DurationMinutes,
            appointment.Status.ToString(),
            appointment.CancellationReason,
            appointment.CreatedAt);
    }
}
