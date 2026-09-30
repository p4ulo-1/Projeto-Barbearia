namespace BackAndre.Domain.Models;

public class Appointment
{
    public const string ConfirmedStatus = "confirmado";
    public const string CanceledStatus = "cancelado";
    public const string CompletedStatus = "concluido";

    private Appointment()
    {
    }

    public Appointment(
        string userId,
        string serviceId,
        string barberId,
        DateOnly date,
        TimeOnly time)
    {
        if (string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(serviceId) ||
            string.IsNullOrWhiteSpace(barberId))
        {
            throw new DomainRuleException("Usuário, serviço e barbeiro são obrigatórios.");
        }

        if (date == default)
        {
            throw new DomainRuleException("A data do agendamento é obrigatória.");
        }

        Id = Guid.NewGuid().ToString("N");
        UserId = userId;
        ServiceId = serviceId;
        BarberId = barberId;
        Date = date;
        Time = time;
        Status = ConfirmedStatus;
        CreatedAt = DateTime.UtcNow;
    }

    public Appointment(
        string id,
        string userId,
        string serviceId,
        string barberId,
        DateOnly date,
        TimeOnly time)
        : this(userId, serviceId, barberId, date, time)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new DomainRuleException("O identificador do agendamento é obrigatório.");
        }

        Id = id;
    }

    public string Id { get; private set; } = string.Empty;
    public string UserId { get; private set; } = string.Empty;
    public string ServiceId { get; private set; } = string.Empty;
    public string BarberId { get; private set; } = string.Empty;
    public DateOnly Date { get; private set; }
    public TimeOnly Time { get; private set; }
    public string Status { get; private set; } = ConfirmedStatus;
    public DateTime CreatedAt { get; private set; }
    public User User { get; private set; } = null!;
    public Service Service { get; private set; } = null!;
    public Barber Barber { get; private set; } = null!;

    public bool IsConfirmed => Status == ConfirmedStatus;

    public void Cancel()
    {
        EnsureConfirmed("Somente agendamentos confirmados podem ser cancelados.");
        Status = CanceledStatus;
    }

    public void Complete(DateTime currentDateTime)
    {
        EnsureConfirmed("Somente agendamentos confirmados podem ser concluídos.");
        if (Date.ToDateTime(Time) > currentDateTime)
        {
            throw new DomainRuleException("Não é possível concluir um atendimento futuro.");
        }

        Status = CompletedStatus;
    }

    public void Reschedule(DateOnly newDate, TimeOnly newTime)
    {
        EnsureConfirmed("Somente agendamentos confirmados podem ser remarcados.");
        if (newDate == default)
        {
            throw new DomainRuleException("A nova data do agendamento é obrigatória.");
        }

        Date = newDate;
        Time = newTime;
    }

    private void EnsureConfirmed(string message)
    {
        if (!IsConfirmed)
        {
            throw new DomainRuleException(message);
        }
    }
}
