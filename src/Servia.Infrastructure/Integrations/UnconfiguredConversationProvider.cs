using Servia.Domain.Ai;

namespace Servia.Infrastructure.Integrations;

/// <summary>
/// Provedor de conversa não configurado (item 1.1).
///
/// <b>Por que existe:</b> o fornecedor de IA ainda não foi escolhido nem
/// configurado. Sem provedor, a aplicação não pode cair — o onboarding
/// responde com um motivo explícito em vez de falhar na inicialização.
///
/// <b>Não simula:</b> não inventa perguntas nem respostas. Toda chamada
/// falha com a causa real (credencial ausente), para que o operador saiba
/// exatamente o que falta configurar. Uma integração simulada faria o
/// produto parecer funcional quando não é.
/// </summary>
public sealed class UnconfiguredConversationProvider : IAiConversationProvider
{
    public string ProviderName => "unconfigured";

    public Task<AiConversationTurn> ContinueAsync(
        AiConversationContext context,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(AiConversationTurn.Failed(
            "O provedor de IA não está configurado. "
            + "Defina Ai:Provider e as credenciais correspondentes."));
}
