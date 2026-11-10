namespace Servia.Api.Contracts;

/// <summary>
/// Contrato de erro devolvido pela API.
///
/// É o único formato de erro exposto ao cliente. Garante que detalhes internos
/// (stack trace, tipo da exceção, caminho de arquivo) nunca vazem na resposta.
/// </summary>
/// <param name="Message">Mensagem legível para o usuário.</param>
/// <param name="TraceId">
/// Identificador da requisição, presente apenas em falhas inesperadas.
/// Permite correlacionar a resposta com o log do servidor sem expor o erro real.
/// </param>
public sealed record ErrorResponse(string Message, string? TraceId = null)
{
    /// <summary>
    /// Resposta para violação de regra de negócio (400).
    /// A mensagem vem do domínio e é segura para o usuário.
    /// </summary>
    public static ErrorResponse FromRuleViolation(string message) => new(message);

    /// <summary>
    /// Resposta para recurso não encontrado (404).
    /// A mensagem é genérica, sem detalhes internos.
    /// </summary>
    public static ErrorResponse FromNotFound() => new("Recurso não encontrado.");

    /// <summary>
    /// Resposta para falha inesperada (500).
    /// Não expõe a causa real; apenas o identificador para correlação.
    /// </summary>
    public static ErrorResponse FromUnexpected(string traceId) =>
        new("Ocorreu um erro inesperado. Informe o identificador ao suporte.", traceId);
}
