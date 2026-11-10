using Servia.Domain.Appointments;

namespace Servia.Application.Appointments;

/// <summary>
/// Resultado de uma operação de agendamento.
///
/// Substitui tuplas por um tipo explícito: o chamador não pode ignorar o
/// estado da operação nem confundir a ordem dos campos. Distingue sucesso,
/// conflito de horário e falha de validação.
/// </summary>
public sealed class AppointmentResult
{
    private AppointmentResult(Appointment? appointment, AppointmentFailure? failure)
    {
        Appointment = appointment;
        Failure = failure;
    }

    /// <summary>Agendamento criado, quando a operação tem sucesso.</summary>
    public Appointment? Appointment { get; }

    /// <summary>Motivo da falha, quando a operação não tem sucesso.</summary>
    public AppointmentFailure? Failure { get; }

    public bool IsSuccess => Failure is null;

    public static AppointmentResult Success(Appointment appointment) =>
        new(appointment, null);

    public static AppointmentResult Conflict(string message) =>
        new(null, new AppointmentFailure(AppointmentFailureKind.Conflict, message));

    public static AppointmentResult Invalid(string message) =>
        new(null, new AppointmentFailure(AppointmentFailureKind.Invalid, message));
}

/// <summary>Natureza da falha ao criar um agendamento.</summary>
public enum AppointmentFailureKind
{
    /// <summary>O pedido viola uma regra de negócio (400).</summary>
    Invalid = 1,

    /// <summary>O horário conflita com outro agendamento (409).</summary>
    Conflict = 2
}

/// <summary>Detalhe da falha.</summary>
public sealed record AppointmentFailure(AppointmentFailureKind Kind, string Message);
