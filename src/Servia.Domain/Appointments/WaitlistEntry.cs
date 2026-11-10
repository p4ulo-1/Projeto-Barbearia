using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Appointments;

/// <summary>
/// Entrada na fila de espera por um horário.
///
/// Quando uma vaga é liberada, o primeiro da fila recebe uma oferta com prazo
/// para aceitar. Se o prazo expirar, a oferta passa para o próximo.
///
/// A fila é por profissional e serviço: o cliente espera por uma combinação
/// específica, não por qualquer horário.
/// </summary>
public sealed class WaitlistEntry : ITenantScoped
{
    public const int DefaultOfferWindowMinutes = 30;

    private WaitlistEntry(
        Guid id,
        Guid tenantId,
        Guid customerId,
        Guid serviceId,
        Guid professionalId,
        DateOnly preferredDate,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        CustomerId = customerId;
        ServiceId = serviceId;
        ProfessionalId = professionalId;
        PreferredDate = preferredDate;
        Status = WaitlistStatus.Waiting;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>.
    /// </summary>
    private WaitlistEntry()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid ServiceId { get; private set; }

    public Guid ProfessionalId { get; private set; }

    public DateOnly PreferredDate { get; private set; }

    public WaitlistStatus Status { get; private set; }

    /// <summary>Horário ofertado, quando houver.</summary>
    public TimeOnly? OfferedTime { get; private set; }

    /// <summary>Prazo para aceitar a oferta.</summary>
    public DateTimeOffset? OfferExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Indica se a entrada ainda aguarda vaga.</summary>
    public bool IsWaiting => Status == WaitlistStatus.Waiting;

    public static WaitlistEntry Create(
        Guid tenantId,
        Guid customerId,
        Guid serviceId,
        Guid professionalId,
        DateOnly preferredDate,
        DateTimeOffset now)
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

        if (preferredDate == default)
        {
            throw new DomainRuleException("A data desejada é obrigatória.");
        }

        return new WaitlistEntry(
            Guid.NewGuid(), tenantId, customerId, serviceId, professionalId, preferredDate, now);
    }

    /// <summary>
    /// Oferta uma vaga liberada ao cliente, com prazo para aceitar.
    /// </summary>
    public void Offer(TimeOnly time, DateTimeOffset now, int windowMinutes = DefaultOfferWindowMinutes)
    {
        if (!IsWaiting)
        {
            throw new DomainRuleException("Somente entradas em espera podem receber oferta.");
        }

        if (windowMinutes <= 0)
        {
            throw new DomainRuleException("O prazo da oferta deve ser maior que zero.");
        }

        Status = WaitlistStatus.Offered;
        OfferedTime = time;
        OfferExpiresAt = now.AddMinutes(windowMinutes);
    }

    /// <summary>Aceita a oferta, convertendo a entrada em agendamento.</summary>
    public void Accept(DateTimeOffset now)
    {
        if (Status != WaitlistStatus.Offered)
        {
            throw new DomainRuleException("Não há oferta ativa para aceitar.");
        }

        if (OfferExpiresAt.HasValue && now >= OfferExpiresAt.Value)
        {
            throw new DomainRuleException("A oferta expirou.");
        }

        Status = WaitlistStatus.Accepted;
    }

    /// <summary>Expira a oferta, liberando a vaga para o próximo da fila.</summary>
    public void ExpireOffer()
    {
        if (Status != WaitlistStatus.Offered)
        {
            throw new DomainRuleException("Não há oferta ativa para expirar.");
        }

        Status = WaitlistStatus.Expired;
        OfferedTime = null;
        OfferExpiresAt = null;
    }

    /// <summary>Remove a entrada da fila por desistência do cliente.</summary>
    public void Withdraw()
    {
        if (Status is WaitlistStatus.Accepted)
        {
            throw new DomainRuleException("Uma entrada já aceita não pode ser retirada.");
        }

        Status = WaitlistStatus.Withdrawn;
    }
}

/// <summary>Estado de uma entrada na fila de espera.</summary>
public enum WaitlistStatus
{
    /// <summary>Aguardando vaga.</summary>
    Waiting = 1,

    /// <summary>Vaga ofertada, aguardando resposta.</summary>
    Offered = 2,

    /// <summary>Oferta aceita.</summary>
    Accepted = 3,

    /// <summary>Oferta expirada.</summary>
    Expired = 4,

    /// <summary>Cliente desistiu.</summary>
    Withdrawn = 5
}
