namespace Servia.Domain.Ai;

/// <summary>
/// Contrato de conversa com a IA (onboarding assistido).
///
/// Separado de <see cref="IAiSiteGenerator"/> por responsabilidade: o
/// gerador produz a configuração final; o provedor de conversa conduz o
/// diálogo que reúne as informações.
///
/// <b>Independência de fornecedor:</b> o domínio não conhece OpenAI,
/// Anthropic ou qualquer SDK. A implementação concreta é escolhida por
/// configuração, e trocar de provedor não altera regra de negócio.
/// </summary>
public interface IAiConversationProvider
{
    /// <summary>Identifica o provedor, para log e auditoria.</summary>
    string ProviderName { get; }

    /// <summary>
    /// Conduz um turno da conversa.
    ///
    /// Recebe o histórico completo e devolve a próxima pergunta da IA,
    /// junto com o que ela já conseguiu extrair. O histórico é enviado
    /// inteiro porque a IA precisa do contexto para não repetir perguntas.
    /// </summary>
    Task<AiConversationTurn> ContinueAsync(
        AiConversationContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Contexto enviado à IA em cada turno.
/// </summary>
public sealed record AiConversationContext(
    string BusinessName,
    string Segment,
    IReadOnlyList<AiConversationMessage> History,
    IReadOnlyDictionary<string, string> CollectedFacts,
    IReadOnlyList<string> MissingFacts);

/// <summary>Mensagem do histórico da conversa.</summary>
public sealed record AiConversationMessage(
    AiConversationRole Role,
    string Content,
    DateTimeOffset SentAt);

/// <summary>Papel de quem enviou a mensagem.</summary>
public enum AiConversationRole
{
    /// <summary>Pergunta ou resposta da IA.</summary>
    Assistant = 1,

    /// <summary>Resposta do proprietário.</summary>
    Owner = 2
}

/// <summary>Resultado de um turno da conversa.</summary>
public sealed record AiConversationTurn(
    bool Success,
    string? Question,
    IReadOnlyDictionary<string, string> ExtractedFacts,
    bool IsComplete,
    int UnitsConsumed,
    string? FailureReason)
{
    public static AiConversationTurn Asked(
        string question,
        IReadOnlyDictionary<string, string> extractedFacts,
        int units) =>
        new(true, question, extractedFacts, false, units, null);

    public static AiConversationTurn Completed(
        IReadOnlyDictionary<string, string> extractedFacts,
        int units) =>
        new(true, null, extractedFacts, true, units, null);

    public static AiConversationTurn Failed(string reason) =>
        new(false, null, new Dictionary<string, string>(), false, 0, reason);
}
