using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Ai;

/// <summary>
/// Registro de uma operação de IA (gestão de IA, §5.4).
///
/// Toda chamada ao provedor é registrada com o consumo real em tokens, o custo
/// conservador calculado e a referência da tabela de preços usada. Isso permite
/// auditar custo, reconciliar com o painel do provedor e identificar abuso.
///
/// <b>Sem conteúdo sensível:</b> não armazena prompt, resposta, segredo nem
/// dados pessoais. Apenas metadados de consumo e resultado.
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
        string model,
        string keyLabel,
        int inputTokens,
        int outputTokens,
        decimal costUsd,
        string priceTableVersion,
        bool success,
        string? failureReason,
        DateTimeOffset occurredAt)
    {
        Id = id;
        TenantId = tenantId;
        OperationType = operationType;
        ProviderName = providerName;
        Model = model;
        KeyLabel = keyLabel;
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        CostUsd = costUsd;
        PriceTableVersion = priceTableVersion;
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
        Model = string.Empty;
        KeyLabel = string.Empty;
        PriceTableVersion = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public AiOperationType OperationType { get; private set; }

    public string ProviderName { get; private set; }

    /// <summary>Modelo usado na operação (§5.4).</summary>
    public string Model { get; private set; }

    /// <summary>
    /// Rótulo da chave usada, para auditoria. Nunca contém o segredo (§3.4).
    /// </summary>
    public string KeyLabel { get; private set; }

    /// <summary>Tokens de entrada informados pelo provedor.</summary>
    public int InputTokens { get; private set; }

    /// <summary>Tokens de saída informados pelo provedor.</summary>
    public int OutputTokens { get; private set; }

    /// <summary>
    /// Total de tokens (entrada + saída). É a base da conversão em créditos
    /// (10.000 tokens por crédito, §5.2).
    /// </summary>
    public int UnitsConsumed => InputTokens + OutputTokens;

    /// <summary>
    /// Custo conservador calculado com os preços de pico (§2.3). Toda entrada
    /// é tratada como cache miss.
    /// </summary>
    public decimal CostUsd { get; private set; }

    /// <summary>
    /// Referência da tabela de preços usada, para auditoria histórica (§2.3).
    /// </summary>
    public string PriceTableVersion { get; private set; }

    public bool Success { get; private set; }

    public string? FailureReason { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public static AiOperationLog Record(
        Guid tenantId,
        AiOperationType operationType,
        string providerName,
        string model,
        string keyLabel,
        int inputTokens,
        int outputTokens,
        decimal costUsd,
        string priceTableVersion,
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

        if (inputTokens < 0 || outputTokens < 0)
        {
            throw new DomainRuleException("O consumo não pode ser negativo.");
        }

        if (costUsd < 0)
        {
            throw new DomainRuleException("O custo não pode ser negativo.");
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
            model?.Trim() ?? string.Empty,
            keyLabel?.Trim() ?? string.Empty,
            inputTokens,
            outputTokens,
            costUsd,
            priceTableVersion?.Trim() ?? string.Empty,
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
    PostLaunchChange = 3,

    /// <summary>Turno da conversa do onboarding assistido.</summary>
    OnboardingConversation = 4
}
