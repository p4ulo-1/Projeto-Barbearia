namespace Servia.Domain.Ai;

/// <summary>
/// Contrato de geração de site por IA (F8.1).
///
/// A IA <b>não gera código</b>: ela seleciona e configura componentes da
/// biblioteca controlada (ADR-007). Isso garante integração com agenda e
/// pagamentos, responsividade e consistência visual.
///
/// O provedor de LLM é escolhido na composição, não aqui. Trocar de provedor
/// é implementar este contrato.
/// </summary>
public interface IAiSiteGenerator
{
    /// <summary>Identifica o provedor, para logs e auditoria.</summary>
    string ProviderName { get; }

    /// <summary>
    /// Rótulo da chave usada na operação, para auditoria (§3.4). Nunca contém
    /// o segredo.
    /// </summary>
    string KeyLabel { get; }

    /// <summary>
    /// Gera uma proposta de site a partir da requisição.
    /// A proposta é um rascunho: só entra no ar após aprovação (F3.6).
    /// </summary>
    Task<AiGenerationResult> GenerateAsync(
        AiGenerationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Aplica um ajuste conversacional sobre uma proposta existente (F8.4).
    /// </summary>
    Task<AiGenerationResult> RefineAsync(
        AiGenerationRequest request,
        AiChangeSet current,
        string instruction,
        CancellationToken cancellationToken = default);
}

/// <summary>Resultado da geração.</summary>
public sealed record AiGenerationResult(
    bool Success,
    AiChangeSet? ChangeSet,
    int InputTokens,
    int OutputTokens,
    string? FailureReason)
{
    /// <summary>
    /// Total de tokens da operação (entrada + saída), exatamente como o
    /// provedor informou. É a base da contabilização em créditos (§5.2).
    /// </summary>
    public int TotalTokens => InputTokens + OutputTokens;

    /// <summary>
    /// Compatibilidade: o consumo em unidades é o total de tokens reais.
    /// </summary>
    public int UnitsConsumed => TotalTokens;

    public static AiGenerationResult Generated(
        AiChangeSet changeSet,
        int inputTokens,
        int outputTokens) =>
        new(true, changeSet, inputTokens, outputTokens, null);

    public static AiGenerationResult Failed(string reason) =>
        new(false, null, 0, 0, reason);
}
