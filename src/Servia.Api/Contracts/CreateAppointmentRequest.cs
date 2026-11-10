using System.ComponentModel.DataAnnotations;

namespace Servia.Api.Contracts;

/// <summary>
/// Corpo da requisição de criação de agendamento.
///
/// A duração não é informada pelo cliente: ela vem do serviço escolhido.
/// Quem agenda não decide quanto tempo o atendimento ocupa.
/// </summary>
public sealed record CreateAppointmentRequest(
    [Required] Guid TenantId,
    [Required] Guid CustomerId,
    [Required] Guid ServiceId,
    [Required] Guid ProfessionalId,
    [Required] DateOnly Date,
    [Required] TimeOnly Time,
    [Range(1, 1440, ErrorMessage = "A duração deve estar entre 1 e 1440 minutos.")]
    int DurationMinutes);
