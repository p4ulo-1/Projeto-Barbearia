namespace Servia.Domain.Appointments;

/// <summary>
/// Estados do ciclo de vida de um agendamento.
/// <see cref="Confirmed"/> é o único estado ativo; os demais são terminais.
/// </summary>
public enum AppointmentStatus
{
    /// <summary>Agendamento ativo, sujeito a cancelamento, conclusão ou remarcação.</summary>
    Confirmed = 1,

    /// <summary>Agendamento desfeito. Estado terminal.</summary>
    Cancelled = 2,

    /// <summary>Atendimento realizado. Estado terminal.</summary>
    Completed = 3
}
