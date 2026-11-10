using Servia.Domain.Appointments;

namespace Servia.Domain.Policies;

/// <summary>
/// Contrato de política de disponibilidade.
///
/// Permite que profissionais com regras de jornada diferentes compartilhem
/// o mesmo ponto de uso sem que o núcleo do sistema conheça as variações.
/// O núcleo depende apenas deste contrato; a implementação é escolhida
/// na composição da aplicação (ADR-018).
/// </summary>
public interface IAvailabilityPolicy
{
    /// <summary>
    /// Indica se o profissional está disponível para o serviço no horário informado.
    /// </summary>
    bool IsAvailable(WorkSchedule schedule, DateOnly date, TimeOnly time, int durationMinutes);

    /// <summary>
    /// Nome legível da política, usado em logs e na configuração.
    /// </summary>
    string Describe();
}
