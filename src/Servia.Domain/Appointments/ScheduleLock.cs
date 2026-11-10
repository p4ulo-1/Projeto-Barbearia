using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Appointments;

/// <summary>
/// Trava de reserva que garante consistência sob concorrência (F2.11).
///
/// A verificação de disponibilidade <b>não</b> é suficiente sozinha: duas
/// requisições simultâneas podem consultar, ambas verem o horário livre e
/// ambas gravarem. A trava resolve isso no banco, com índice único parcial em
/// <c>(TenantId, ProfessionalId, Date, StartTime)</c>.
///
/// A trava tem expiração: se a transação não concluir, ela deixa de bloquear
/// a agenda.
/// </summary>
public sealed class ScheduleLock : ITenantScoped
{
    public const int DefaultLifetimeMinutes = 5;

    private ScheduleLock(
        Guid id,
        Guid tenantId,
        Guid professionalId,
        DateOnly date,
        TimeOnly startTime,
        TimeOnly endTime,
        DateTimeOffset expiresAt)
    {
        Id = id;
        TenantId = tenantId;
        ProfessionalId = professionalId;
        Date = date;
        StartTime = startTime;
        EndTime = endTime;
        ExpiresAt = expiresAt;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Acquire"/>.
    /// </summary>
    private ScheduleLock()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ProfessionalId { get; private set; }

    public DateOnly Date { get; private set; }

    public TimeOnly StartTime { get; private set; }

    public TimeOnly EndTime { get; private set; }

    /// <summary>Agendamento que adquiriu a trava, quando concluído.</summary>
    public Guid? AppointmentId { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Indica se a trava ainda está ativa.</summary>
    public bool IsActive(DateTimeOffset now) => now < ExpiresAt;

    /// <summary>
    /// Adquire a trava para um horário.
    /// O índice único parcial no banco rejeita a segunda trava concorrente.
    /// </summary>
    public static ScheduleLock Acquire(
        Guid tenantId,
        Guid professionalId,
        DateOnly date,
        TimeOnly startTime,
        int durationMinutes,
        DateTimeOffset now,
        int lifetimeMinutes = DefaultLifetimeMinutes)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (professionalId == Guid.Empty)
        {
            throw new DomainRuleException("O profissional é obrigatório.");
        }

        if (date == default)
        {
            throw new DomainRuleException("A data é obrigatória.");
        }

        if (durationMinutes <= 0)
        {
            throw new DomainRuleException("A duração deve ser maior que zero.");
        }

        if (lifetimeMinutes <= 0)
        {
            throw new DomainRuleException("A validade da trava deve ser maior que zero.");
        }

        var endTime = startTime.AddMinutes(durationMinutes);

        return new ScheduleLock(
            Guid.NewGuid(),
            tenantId,
            professionalId,
            date,
            startTime,
            endTime,
            now.AddMinutes(lifetimeMinutes));
    }

    /// <summary>Vincula a trava ao agendamento criado.</summary>
    public void AttachTo(Guid appointmentId)
    {
        if (appointmentId == Guid.Empty)
        {
            throw new DomainRuleException("O agendamento é obrigatório.");
        }

        AppointmentId = appointmentId;
    }

    /// <summary>Libera a trava, permitindo que o horário seja reservado.</summary>
    public void Release() => ExpiresAt = DateTimeOffset.MinValue;
}
