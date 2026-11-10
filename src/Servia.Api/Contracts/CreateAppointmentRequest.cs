using System.ComponentModel.DataAnnotations;

namespace Servia.Api.Contracts;

/// <summary>
/// Corpo da requisição de criação de agendamento.
///
/// O tenant <b>não</b> faz parte do corpo: ele é resolvido no servidor
/// (ARCH.md §3.2). Aceitá-lo do cliente permitiria acessar dados de outro
/// estabelecimento.
///
/// A duração não é informada pelo cliente: ela vem do serviço escolhido.
/// Quem agenda não decide quanto tempo o atendimento ocupa.
/// </summary>
public sealed record CreateAppointmentRequest(
    [Required] Guid CustomerId,
    [Required] Guid ServiceId,
    [Required] Guid ProfessionalId,
    [Required] DateOnly Date,
    [Required] TimeOnly Time,
    [Range(1, 1440, ErrorMessage = "A duração deve estar entre 1 e 1440 minutos.")]
    int DurationMinutes);
