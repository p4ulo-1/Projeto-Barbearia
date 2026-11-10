using Servia.Domain.Common;

namespace Servia.Domain.Policies;

/// <summary>
/// Política de sessão longa com sinal (F2.9, F2.10).
///
/// Usada por segmentos com sessões prolongadas e alto custo de no-show:
/// tatuagem, massoterapia, estética avançada. Exige antecedência maior,
/// sinal de 30% e permite sessões de até 8 horas.
///
/// O sinal existe porque o custo de uma falta é alto: o profissional reserva
/// o dia inteiro e não consegue preencher a vaga em cima da hora.
/// </summary>
public sealed class LongSessionBookingPolicy : IBookingPolicy
{
    public int MinimumLeadTimeMinutes => 24 * 60;

    public int MinimumCancellationNoticeMinutes => 48 * 60;

    public bool RequiresDeposit => true;

    public decimal DepositPercentage => 0.30m;

    public int MaxSessionMinutes => 8 * 60;

    public void EnsureLeadTime(DateTimeOffset appointmentStart, DateTimeOffset now)
    {
        var minimumStart = now.AddMinutes(MinimumLeadTimeMinutes);

        if (appointmentStart < minimumStart)
        {
            throw new DomainRuleException(
                "Sessões longas exigem agendamento com pelo menos 24 horas de antecedência.");
        }
    }

    public void EnsureSessionDuration(int durationMinutes)
    {
        if (durationMinutes <= 0)
        {
            throw new DomainRuleException("A duração do atendimento deve ser maior que zero.");
        }

        if (durationMinutes > MaxSessionMinutes)
        {
            throw new DomainRuleException(
                $"A sessão não pode exceder {MaxSessionMinutes / 60} horas.");
        }
    }

    public string Describe() => "Sessão longa com sinal";
}
