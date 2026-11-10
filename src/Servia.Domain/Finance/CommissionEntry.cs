using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Finance;

/// <summary>
/// Comissão apurada de um profissional por atendimento (F5.6, F5.7).
///
/// É o registro do que o profissional tem a receber. O extrato (F5.7) é a
/// consulta dessas entradas por período.
///
/// O ciclo é: apurada ao concluir o atendimento, aprovada pelo
/// estabelecimento e paga. Uma comissão paga não pode ser alterada.
/// </summary>
public sealed class CommissionEntry : ITenantScoped
{
    private CommissionEntry(
        Guid id,
        Guid tenantId,
        Guid professionalId,
        Guid appointmentId,
        Money amount,
        DateOnly referencePeriod,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        ProfessionalId = professionalId;
        AppointmentId = appointmentId;
        Amount = amount;
        ReferencePeriod = referencePeriod;
        Status = CommissionStatus.Pending;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>.
    /// </summary>
    private CommissionEntry()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ProfessionalId { get; private set; }

    public Guid AppointmentId { get; private set; }

    public Money Amount { get; private set; }

    /// <summary>Período de referência, usado no extrato.</summary>
    public DateOnly ReferencePeriod { get; private set; }

    public CommissionStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? PaidAt { get; private set; }

    /// <summary>Indica se a comissão já foi paga.</summary>
    public bool IsPaid => Status == CommissionStatus.Paid;

    public static CommissionEntry Create(
        Guid tenantId,
        Guid professionalId,
        Guid appointmentId,
        Money amount,
        DateOnly referencePeriod,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (professionalId == Guid.Empty)
        {
            throw new DomainRuleException("O profissional é obrigatório.");
        }

        if (appointmentId == Guid.Empty)
        {
            throw new DomainRuleException("O atendimento é obrigatório.");
        }

        if (amount.Amount < 0)
        {
            throw new DomainRuleException("A comissão não pode ser negativa.");
        }

        if (referencePeriod == default)
        {
            throw new DomainRuleException("O período de referência é obrigatório.");
        }

        return new CommissionEntry(
            Guid.NewGuid(), tenantId, professionalId, appointmentId, amount, referencePeriod, now);
    }

    /// <summary>Aprova a comissão para pagamento.</summary>
    public void Approve()
    {
        if (Status != CommissionStatus.Pending)
        {
            throw new DomainRuleException("Somente comissões pendentes podem ser aprovadas.");
        }

        Status = CommissionStatus.Approved;
    }

    /// <summary>Registra o pagamento da comissão.</summary>
    public void MarkPaid(DateTimeOffset now)
    {
        if (Status != CommissionStatus.Approved)
        {
            throw new DomainRuleException("Somente comissões aprovadas podem ser pagas.");
        }

        Status = CommissionStatus.Paid;
        PaidAt = now;
    }

    /// <summary>Cancela a comissão, antes do pagamento.</summary>
    public void Cancel()
    {
        if (IsPaid)
        {
            throw new DomainRuleException("Uma comissão paga não pode ser cancelada.");
        }

        Status = CommissionStatus.Cancelled;
    }
}

/// <summary>Estado da comissão.</summary>
public enum CommissionStatus
{
    Pending = 1,
    Approved = 2,
    Paid = 3,
    Cancelled = 4
}
