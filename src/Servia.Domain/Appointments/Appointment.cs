using Servia.Domain.Common;
using Servia.Domain.Policies;

namespace Servia.Domain.Appointments;

/// <summary>
/// Agregado raiz do atendimento.
///
/// Protege as invariantes de estado: apenas agendamentos <see cref="AppointmentStatus.Confirmed"/>
/// podem ser cancelados, concluídos ou remarcados. Os estados <see cref="AppointmentStatus.Cancelled"/>
/// e <see cref="AppointmentStatus.Completed"/> são terminais.
///
/// A identidade (<see cref="Id"/>) é preservada em todas as transições.
/// </summary>
public sealed class Appointment
{
    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>,
    /// que é o único caminho público de construção.
    /// </summary>
    private Appointment()
    {
    }

    private Appointment(
        Guid id,
        Guid tenantId,
        Guid customerId,
        Guid serviceId,
        Guid professionalId,
        DateOnly date,
        TimeOnly time,
        int durationMinutes,
        AppointmentStatus status,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        CustomerId = customerId;
        ServiceId = serviceId;
        ProfessionalId = professionalId;
        Date = date;
        Time = time;
        DurationMinutes = durationMinutes;
        Status = status;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid ServiceId { get; private set; }

    public Guid ProfessionalId { get; private set; }

    public DateOnly Date { get; private set; }

    public TimeOnly Time { get; private set; }

    public int DurationMinutes { get; private set; }

    public AppointmentStatus Status { get; private set; }

    public string? CancellationReason { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Indica se o agendamento está ativo.</summary>
    public bool IsConfirmed => Status == AppointmentStatus.Confirmed;

    /// <summary>Intervalo ocupado pelo atendimento, comparável mesmo cruzando a meia-noite.</summary>
    public TimeRange OccupiedRange => TimeRange.From(Time, DurationMinutes);

    /// <summary>Instante de início do atendimento.</summary>
    public DateTimeOffset StartsAt => new(Date.ToDateTime(Time), TimeSpan.Zero);

    /// <summary>
    /// Cria um agendamento no estado <see cref="AppointmentStatus.Confirmed"/>.
    /// </summary>
    public static Appointment Create(
        Guid tenantId,
        Guid customerId,
        Guid serviceId,
        Guid professionalId,
        DateOnly date,
        TimeOnly time,
        int durationMinutes,
        DateTimeOffset now,
        IOvernightPolicy? overnightPolicy = null)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (customerId == Guid.Empty)
        {
            throw new DomainRuleException("O cliente é obrigatório.");
        }

        if (serviceId == Guid.Empty)
        {
            throw new DomainRuleException("O serviço é obrigatório.");
        }

        if (professionalId == Guid.Empty)
        {
            throw new DomainRuleException("O profissional é obrigatório.");
        }

        if (date == default)
        {
            throw new DomainRuleException("A data do agendamento é obrigatória.");
        }

        if (durationMinutes <= 0)
        {
            throw new DomainRuleException("A duração do atendimento deve ser maior que zero.");
        }

        // A politica decide se o atendimento pode ultrapassar a meia-noite.
        // O padrao e restritivo: sem politica explicita, nao permite.
        var policy = overnightPolicy ?? new NoOvernightPolicy();
        policy.EnsureAllowed(TimeRange.From(time, durationMinutes));

        return new Appointment(
            Guid.NewGuid(),
            tenantId,
            customerId,
            serviceId,
            professionalId,
            date,
            time,
            durationMinutes,
            AppointmentStatus.Confirmed,
            now);
    }

    /// <summary>
    /// Transição <see cref="AppointmentStatus.Confirmed"/> → <see cref="AppointmentStatus.Cancelled"/>.
    /// </summary>
    public void Cancel(string? reason = null)
    {
        EnsureConfirmed("Somente agendamentos confirmados podem ser cancelados.");

        Status = AppointmentStatus.Cancelled;
        CancellationReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    /// <summary>
    /// Transição <see cref="AppointmentStatus.Confirmed"/> → <see cref="AppointmentStatus.Completed"/>.
    /// Rejeita atendimentos cujo horário ainda não ocorreu.
    /// </summary>
    public void Complete(DateTimeOffset now)
    {
        EnsureConfirmed("Somente agendamentos confirmados podem ser concluídos.");

        if (StartsAt > now)
        {
            throw new DomainRuleException("Não é possível concluir um atendimento futuro.");
        }

        Status = AppointmentStatus.Completed;
    }

    /// <summary>
    /// Altera data e horário, mantendo o estado <see cref="AppointmentStatus.Confirmed"/>.
    /// </summary>
    public void Reschedule(DateOnly date, TimeOnly time)
    {
        EnsureConfirmed("Somente agendamentos confirmados podem ser remarcados.");

        if (date == default)
        {
            throw new DomainRuleException("A nova data do agendamento é obrigatória.");
        }

        Date = date;
        Time = time;
    }

    /// <summary>
    /// Guarda da invariante de estado. Toda transição passa por aqui.
    /// </summary>
    private void EnsureConfirmed(string message)
    {
        if (!IsConfirmed)
        {
            throw new DomainRuleException(message);
        }
    }
}
