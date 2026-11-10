using Servia.Domain.Common;

namespace Servia.Domain.Policies;

/// <summary>
/// Contrato de política de atendimento noturno.
///
/// Define se o estabelecimento aceita atendimentos que terminam após a
/// meia-noite. A variação é de <b>comportamento</b>, não de dado: por isso é
/// uma política injetada, e não um booleano no agendamento (ADR-018).
///
/// Segmentos com sessões longas — tatuagem, massoterapia — habilitam a
/// permissão. Barbearias e salões usam a política padrão, que proíbe.
/// </summary>
public interface IOvernightPolicy
{
    /// <summary>
    /// Indica se o intervalo pode terminar após a meia-noite.
    /// </summary>
    bool AllowsOvernight { get; }

    /// <summary>
    /// Valida o intervalo segundo a política.
    /// Lança <see cref="DomainRuleException"/> quando o intervalo é proibido.
    /// </summary>
    void EnsureAllowed(TimeRange range);

    /// <summary>Nome legível da política, usado em logs e na configuração.</summary>
    string Describe();
}
