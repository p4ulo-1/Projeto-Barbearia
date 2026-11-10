using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Auditing;

/// <summary>
/// Registro imutável de uma ação sensível (ADR-013).
///
/// A entidade <b>não possui métodos de alteração ou exclusão</b>: uma vez
/// gravada, a trilha não pode ser reescrita. Isso é o que torna a auditoria
/// confiável para suporte, segurança e conformidade.
///
/// <see cref="TenantId"/> é nulo para ações da plataforma, que não pertencem
/// a um estabelecimento.
/// </summary>
public sealed class AuditEntry
{
    private AuditEntry(
        Guid id,
        Guid? tenantId,
        Guid? actorId,
        string action,
        string entityType,
        Guid entityId,
        string? changes,
        DateTimeOffset occurredAt,
        string? ipAddress)
    {
        Id = id;
        TenantId = tenantId;
        ActorId = actorId;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        Changes = changes;
        OccurredAt = occurredAt;
        IpAddress = ipAddress;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Record"/>.
    /// </summary>
    private AuditEntry()
    {
        Action = string.Empty;
        EntityType = string.Empty;
    }

    public Guid Id { get; private set; }

    /// <summary>Estabelecimento afetado. Nulo para ações da plataforma.</summary>
    public Guid? TenantId { get; private set; }

    /// <summary>Usuário que executou a ação. Nulo para ações do sistema.</summary>
    public Guid? ActorId { get; private set; }

    /// <summary>Ação executada, no formato <c>Entidade.Acao</c>.</summary>
    public string Action { get; private set; }

    /// <summary>Tipo da entidade afetada.</summary>
    public string EntityType { get; private set; }

    /// <summary>Identificador do registro afetado.</summary>
    public Guid EntityId { get; private set; }

    /// <summary>Detalhes da alteração, em JSON. Nulo quando não aplicável.</summary>
    public string? Changes { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Origem da requisição, quando disponível.</summary>
    public string? IpAddress { get; private set; }

    /// <summary>
    /// Registra uma ação sensível. Único caminho de criação.
    /// </summary>
    public static AuditEntry Record(
        string action,
        string entityType,
        Guid entityId,
        DateTimeOffset now,
        Guid? tenantId = null,
        Guid? actorId = null,
        string? changes = null,
        string? ipAddress = null)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new DomainRuleException("A ação auditada é obrigatória.");
        }

        if (string.IsNullOrWhiteSpace(entityType))
        {
            throw new DomainRuleException("O tipo da entidade auditada é obrigatório.");
        }

        if (entityId == Guid.Empty)
        {
            throw new DomainRuleException("O identificador da entidade auditada é obrigatório.");
        }

        return new AuditEntry(
            Guid.NewGuid(),
            tenantId,
            actorId,
            action.Trim(),
            entityType.Trim(),
            entityId,
            string.IsNullOrWhiteSpace(changes) ? null : changes,
            now,
            string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress.Trim());
    }

    /// <summary>Indica se a ação foi executada pela plataforma, sem tenant.</summary>
    public bool IsPlatformAction => !TenantId.HasValue;
}
