using Servia.Domain.Common;

namespace Servia.Domain.Policies;

/// <summary>
/// Política padrão: o atendimento precisa terminar até a meia-noite.
///
/// Usada por segmentos com atendimento diurno — barbearias, salões, estúdios
/// de unhas. Proíbe qualquer intervalo que ultrapasse o fim do dia, evitando
/// a ambiguidade de um agendamento que pertence a uma data e termina em outra.
/// </summary>
public sealed class NoOvernightPolicy : IOvernightPolicy
{
    public bool AllowsOvernight => false;

    public void EnsureAllowed(TimeRange range)
    {
        if (range.EndMinutes > TimeRange.MinutesPerDay)
        {
            throw new DomainRuleException(
                "O atendimento não pode terminar após a meia-noite.");
        }
    }

    public string Describe() => "Atendimento apenas dentro do dia";
}
