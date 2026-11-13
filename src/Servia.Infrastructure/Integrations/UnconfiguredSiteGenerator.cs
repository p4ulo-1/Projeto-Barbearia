using Servia.Domain.Ai;

namespace Servia.Infrastructure.Integrations;

/// <summary>
/// Gerador de site não configurado (item 1.1).
///
/// Sem credencial de IA, a geração responde com motivo explícito em vez de
/// cair. Não simula proposta: uma configuração inventada seria publicada
/// como se fosse real.
/// </summary>
public sealed class UnconfiguredSiteGenerator : IAiSiteGenerator
{
    public string ProviderName => "unconfigured";

    public Task<AiGenerationResult> GenerateAsync(
        AiGenerationRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(AiGenerationResult.Failed(
            "O gerador de IA não está configurado. Defina OpenAi:ApiKey."));

    public Task<AiGenerationResult> RefineAsync(
        AiGenerationRequest request,
        AiChangeSet current,
        string instruction,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(AiGenerationResult.Failed(
            "O gerador de IA não está configurado. Defina OpenAi:ApiKey."));
}
