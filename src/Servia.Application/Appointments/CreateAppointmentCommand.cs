namespace Servia.Application.Appointments;

/// <summary>
/// Dados necessários para criar um agendamento.
///
/// <b>A duração não faz parte do comando.</b> Ela é lida do serviço
/// persistido dentro da transação de criação. Aceitá-la do cliente permitiria
/// reservar um intervalo menor que o real e sobrepor outro atendimento.
/// </summary>
public sealed record CreateAppointmentCommand(
    Guid TenantId,
    Guid CustomerId,
    Guid ServiceId,
    Guid ProfessionalId,
    DateOnly Date,
    TimeOnly Time);