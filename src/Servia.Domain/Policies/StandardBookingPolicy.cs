using Servia.Domain.Common;

namespace Servia.Domain.Policies;

/// <summary>
/// Política padrão de agendamento.
///
/// Usada por segmentos com atendimento diurno e sessões curtas: barbearias,
/// salões, estúdios de unhas. Exige antecedência de 30 minutos, não exige
/// sinal e limita a sessão a 4 horas.
/// </summary>
public sealed class StandardBookingPolicy : IBookingPolicy
{
    public int MinimumLeadTimeMinutes => 30;

    public int MinimumCancellationNoticeMinutes => 60;

    public bool RequiresDeposit => false;

    public decimal DepositPercentage => 0m;

    public int MaxSessionMinutes => 4 * 60;

    public void EnsureLeadTime(DateTimeOffset appointmentStart, DateTimeOffset now)
    {
        var minimumStart = now.AddMinutes(MinimumLeadTimeMinutes);

        if (appointmentStart < minimumStart)
        {
            throw new DomainRuleException(
                $"Escolha um horário com pelo menos {MinimumLeadTimeMinutes} minutos de antecedência.");
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

    public string Describe() => "Agendamento padrão";
}
