namespace Servia.Application.Appointments;

/// <summary>
/// Dados necessários para criar um agendamento.
///
/// A duração é informada pelo serviço, não pelo cliente: quem agenda não
/// escolhe quanto tempo o atendimento ocupa.
/// </summary>
public sealed record CreateAppointmentCommand(
    Guid TenantId,
    Guid CustomerId,
    Guid ServiceId,
    Guid ProfessionalId,
    DateOnly Date,
    TimeOnly Time,
    int DurationMinutes);
