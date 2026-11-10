using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Ai;

/// <summary>
/// Registro de uma operação de IA (F8.5).
///
/// Toda chamada ao provedor é registrada com o consumo em unidades. Isso
/// permite auditar custo, identificar abuso e alimentar a cobrança (F4.17).
///
/// O log é imutável: não há método de alteração.
/// </summary>
public sealed class AiOperationLog : ITenantScoped
{
    private AiOperationLog(
        Guid id,
        Guid tenantId,
        AiOperationType operationType,
        string providerName,
        int unitsConsumed,
        bool success,
        string? failureReason,
        DateTimeOffset occurredAt)
    {
        Id = id;
        TenantId = tenantId;
        OperationType = operationType;
        ProviderName = providerName;
        UnitsConsumed = unitsConsumed;
        Success = success;
        FailureReason = failureReason;
        OccurredAt = occurredAt;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Record"/>.
    /// </summary>
    private AiOperationLog()
    {
        ProviderName = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public AiOperationType OperationType { get; private set; }

    public string ProviderName { get; private set; }

    /// <summary>Unidades de consumo. Zero quando a operação falhou antes de consumir.</summary>
    public int UnitsConsumed { get; private set; }

    public bool Success { get; private set; }

    public string? FailureReason { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public static AiOperationLog Record(
        Guid tenantId,
        AiOperationType operationType,
        string providerName,
        int unitsConsumed,
        bool success,
        DateTimeOffset now,
        string? failureReason = null)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new DomainRuleException("O provedor é obrigatório.");
        }

        if (unitsConsumed < 0)
        {
            throw new DomainRuleException("O consumo não pode ser negativo.");
        }

        if (!success && string.IsNullOrWhiteSpace(failureReason))
        {
            throw new DomainRuleException("Uma falha deve registrar o motivo.");
        }

        return new AiOperationLog(
            Guid.NewGuid(),
            tenantId,
            operationType,
            providerName.Trim(),
            unitsConsumed,
            success,
            string.IsNullOrWhiteSpace(failureReason) ? null : failureReason.Trim(),
            now);
    }
}

/// <summary>Tipo de operação de IA.</summary>
public enum AiOperationType
{
    /// <summary>Geração inicial do site.</summary>
    SiteGeneration = 1,

    /// <summary>Ajuste conversacional sobre uma proposta (F8.4).</summary>
    SiteRefinement = 2,

    /// <summary>Alteração solicitada após a publicação (F8.8).</summary>
    PostLaunchChange = 3
}
