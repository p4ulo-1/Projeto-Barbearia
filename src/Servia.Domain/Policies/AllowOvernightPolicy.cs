using Servia.Domain.Common;

namespace Servia.Domain.Policies;

/// <summary>
/// Política de sessão longa: permite atendimento que termina após a meia-noite.
///
/// Usada por segmentos com sessões prolongadas — tatuagem, massoterapia,
/// tranças. O atendimento pertence à data de início, mesmo que termine no dia
/// seguinte; a detecção de conflito considera o intervalo real.
///
/// Há um limite de segurança: a sessão não pode ultrapassar
/// <see cref="MaxOvernightMinutes"/> além da meia-noite, para evitar
/// agendamentos com duração absurda por erro de entrada.
/// </summary>
public sealed class AllowOvernightPolicy : IOvernightPolicy
{
    /// <summary>Máximo de minutos permitidos após a meia-noite (4 horas).</summary>
    public const int MaxOvernightMinutes = 4 * 60;

    public bool AllowsOvernight => true;

    public void EnsureAllowed(TimeRange range)
    {
        var overflow = range.EndMinutes - TimeRange.MinutesPerDay;

        if (overflow > MaxOvernightMinutes)
        {
            throw new DomainRuleException(
                $"O atendimento não pode ultrapassar {MaxOvernightMinutes / 60} horas após a meia-noite.");
        }
    }

    public string Describe() => "Sessão longa (permite ultrapassar a meia-noite)";
}
