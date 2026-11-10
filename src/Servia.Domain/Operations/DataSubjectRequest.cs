using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Operations;

/// <summary>
/// Solicitação do titular de dados (F9.4).
///
/// Atende aos direitos da LGPD: acesso, correção, exclusão e portabilidade.
/// O estabelecimento é o <b>controlador</b>; a plataforma é a
/// <b>operadora</b> (ADR-012). A solicitação é registrada no tenant do
/// controlador.
///
/// A exclusão não apaga o histórico financeiro: os dados pessoais são
/// anonimizados, preservando a integridade contábil.
/// </summary>
public sealed class DataSubjectRequest : ITenantScoped
{
    private DataSubjectRequest(
        Guid id,
        Guid tenantId,
        Guid customerId,
        DataSubjectRequestType requestType,
        DateTimeOffset requestedAt)
    {
        Id = id;
        TenantId = tenantId;
        CustomerId = customerId;
        RequestType = requestType;
        RequestedAt = requestedAt;
        Status = DataSubjectRequestStatus.Pending;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>.
    /// </summary>
    private DataSubjectRequest()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid CustomerId { get; private set; }

    public DataSubjectRequestType RequestType { get; private set; }

    public DataSubjectRequestStatus Status { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>Prazo legal para atendimento: 15 dias.</summary>
    public DateTimeOffset DueAt => RequestedAt.AddDays(15);

    /// <summary>Indica se o prazo legal foi excedido.</summary>
    public bool IsOverdue(DateTimeOffset now) =>
        Status == DataSubjectRequestStatus.Pending && now > DueAt;

    public static DataSubjectRequest Create(
        Guid tenantId,
        Guid customerId,
        DataSubjectRequestType requestType,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (customerId == Guid.Empty)
        {
            throw new DomainRuleException("O titular é obrigatório.");
        }

        return new DataSubjectRequest(Guid.NewGuid(), tenantId, customerId, requestType, now);
    }

    /// <summary>Registra o atendimento da solicitação.</summary>
    public void Complete(DateTimeOffset now)
    {
        if (Status == DataSubjectRequestStatus.Completed)
        {
            throw new DomainRuleException("A solicitação já foi atendida.");
        }

        Status = DataSubjectRequestStatus.Completed;
        CompletedAt = now;
    }

    /// <summary>Recusa a solicitação, com fundamento registrado.</summary>
    public void Reject(string reason)
    {
        if (Status == DataSubjectRequestStatus.Completed)
        {
            throw new DomainRuleException("Uma solicitação atendida não pode ser recusada.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainRuleException("O fundamento da recusa é obrigatório.");
        }

        Status = DataSubjectRequestStatus.Rejected;
    }
}

/// <summary>Tipo de solicitação do titular.</summary>
public enum DataSubjectRequestType
{
    /// <summary>Acesso aos dados.</summary>
    Access = 1,

    /// <summary>Correção de dados.</summary>
    Correction = 2,

    /// <summary>Exclusão ou anonimização.</summary>
    Deletion = 3,

    /// <summary>Portabilidade.</summary>
    Portability = 4
}

/// <summary>Estado da solicitação.</summary>
public enum DataSubjectRequestStatus
{
    Pending = 1,
    Completed = 2,
    Rejected = 3
}
