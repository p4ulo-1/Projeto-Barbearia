using Servia.Domain.Appointments;
using Servia.Domain.Common;

namespace Servia.Application.Appointments;

/// <summary>
/// Casos de uso de agendamento.
///
/// Orquestra o domínio e o repositório. A regra de não sobreposição (R5) vive
/// aqui porque depende do conjunto de agendamentos do profissional — não pode
/// ser protegida por uma única entidade sem que ela passe a depender de dados
/// externos (ver docs/av2/analise-do-modelo.md, Decisão 1).
///
/// A comparação de intervalos usa <see cref="TimeRange"/>, que trabalha em
/// minutos desde a meia-noite e portanto não quebra quando o atendimento
/// cruza a meia-noite (DEF-001).
/// </summary>
public sealed class AppointmentService(
    IAppointmentRepository repository,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Cria um agendamento, rejeitando conflito de horário.
    /// </summary>
    public async Task<AppointmentResult> CreateAsync(
        CreateAppointmentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.DurationMinutes <= 0)
        {
            return AppointmentResult.Invalid("A duração do atendimento deve ser maior que zero.");
        }

        var occupied = TimeRange.From(command.Time, command.DurationMinutes);

        var existing = await repository.GetActiveByProfessionalAndDateAsync(
            command.TenantId,
            command.ProfessionalId,
            command.Date,
            cancellationToken);

        if (HasOverlap(existing, occupied))
        {
            return AppointmentResult.Conflict("Este horário conflita com outro agendamento.");
        }

        Appointment appointment;

        try
        {
            appointment = Appointment.Create(
                command.TenantId,
                command.CustomerId,
                command.ServiceId,
                command.ProfessionalId,
                command.Date,
                command.Time,
                command.DurationMinutes,
                timeProvider.GetUtcNow());
        }
        catch (DomainRuleException exception)
        {
            return AppointmentResult.Invalid(exception.Message);
        }

        await repository.AddAsync(appointment, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return AppointmentResult.Success(appointment);
    }

    /// <summary>
    /// Indica se o intervalo pretendido se sobrepõe a algum agendamento ativo.
    ///
    /// Regra R5: dois agendamentos do mesmo profissional não podem ocupar
    /// intervalos sobrepostos. Agendamentos cancelados não bloqueiam o horário.
    /// </summary>
    internal static bool HasOverlap(
        IEnumerable<Appointment> existing,
        TimeRange candidate)
    {
        ArgumentNullException.ThrowIfNull(existing);

        return existing.Any(appointment =>
            appointment.IsConfirmed && appointment.OccupiedRange.Overlaps(candidate));
    }
}
