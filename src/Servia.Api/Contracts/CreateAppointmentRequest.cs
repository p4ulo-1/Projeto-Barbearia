using System.ComponentModel.DataAnnotations;

namespace Servia.Api.Contracts;

/// <summary>
/// Corpo da requisição de criação de agendamento.
///
/// O tenant <b>não</b> faz parte do corpo: ele é resolvido no servidor
/// (ARCH.md §3.2). Aceitá-lo do cliente permitiria acessar dados de outro
/// estabelecimento.
///
/// <b>A duração não é informada pelo cliente.</b> Ela vem do serviço
/// escolhido, lido no backend. Quem agenda não decide quanto tempo o
/// atendimento ocupa — aceitar o valor permitiria reservar um intervalo
/// menor que o real e sobrepor outro atendimento.
/// </summary>
public sealed record CreateAppointmentRequest(
    [Required] Guid CustomerId,
    [Required] Guid ServiceId,
    [Required] Guid ProfessionalId,
    [Required] DateOnly Date,
    [Required] TimeOnly Time);