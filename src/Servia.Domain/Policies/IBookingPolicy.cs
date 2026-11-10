using Servia.Domain.Common;

namespace Servia.Domain.Policies;

/// <summary>
/// Contrato de política de agendamento (F2.7, F2.9, F2.10).
///
/// Concentra as regras que variam por segmento: antecedência mínima, sinal
/// para reserva e suporte a sessões prolongadas. O núcleo depende apenas
/// deste contrato; a implementação é escolhida na composição (ADR-018).
/// </summary>
public interface IBookingPolicy
{
    /// <summary>Antecedência mínima, em minutos, para agendar.</summary>
    int MinimumLeadTimeMinutes { get; }

    /// <summary>Antecedência mínima, em minutos, para cancelar.</summary>
    int MinimumCancellationNoticeMinutes { get; }

    /// <summary>Indica se o agendamento exige sinal antecipado.</summary>
    bool RequiresDeposit { get; }

    /// <summary>Percentual do valor do serviço exigido como sinal.</summary>
    decimal DepositPercentage { get; }

    /// <summary>Duração máxima de uma sessão, em minutos.</summary>
    int MaxSessionMinutes { get; }

    /// <summary>
    /// Valida a antecedência do agendamento.
    /// Lança <see cref="DomainRuleException"/> quando o horário é próximo demais.
    /// </summary>
    void EnsureLeadTime(DateTimeOffset appointmentStart, DateTimeOffset now);

    /// <summary>
    /// Valida a duração da sessão.
    /// Lança <see cref="DomainRuleException"/> quando excede o máximo permitido.
    /// </summary>
    void EnsureSessionDuration(int durationMinutes);

    /// <summary>Nome legível da política.</summary>
    string Describe();
}
