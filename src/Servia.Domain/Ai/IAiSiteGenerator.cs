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
    int UnitsConsumed,
    string? FailureReason)
{
    public static AiGenerationResult Generated(AiChangeSet changeSet, int units) =>
        new(true, changeSet, units, null);

    public static AiGenerationResult Failed(string reason) =>
        new(false, null, 0, reason);
}
