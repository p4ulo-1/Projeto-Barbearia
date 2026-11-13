using Servia.Domain.Ai;

namespace Servia.Domain.Sites;

/// <summary>
/// Contrato de persistência do onboarding assistido por IA (itens 1 e 2).
///
/// <b>Por que no domínio:</b> a infraestrutura implementa sem depender da
/// aplicação (ADR-004). A aplicação depende deste contrato, nunca do EF Core.
///
/// <b>Isolamento:</b> todas as operações recebem o <c>tenantId</c> como
/// parâmetro explícito, além do filtro global. Um repositório nunca expõe
/// um método que atravesse a fronteira de estabelecimentos.
/// </summary>
public interface IOnboardingRepository
{
    /// <summary>
    /// Retorna a sessão de onboarding ainda em andamento do tenant, quando
    /// existir. Permite retomar o processo sem perder as respostas já dadas.
    /// </summary>
    Task<OnboardingSession?> FindOpenSessionAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Busca uma sessão do tenant pelo identificador.</summary>
    Task<OnboardingSession?> FindSessionAsync(
        Guid tenantId,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>Lista as sessões de onboarding do tenant, da mais recente para a mais antiga.</summary>
    Task<IReadOnlyList<OnboardingSession>> ListSessionsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Registra uma nova sessão.</summary>
    Task AddSessionAsync(OnboardingSession session, CancellationToken cancellationToken = default);

    /// <summary>Busca uma proposta de configuração gerada pela IA.</summary>
    Task<AiChangeSet?> FindChangeSetAsync(
        Guid tenantId,
        Guid changeSetId,
        CancellationToken cancellationToken = default);

    /// <summary>Registra uma nova proposta de configuração.</summary>
    Task AddChangeSetAsync(AiChangeSet changeSet, CancellationToken cancellationToken = default);

    /// <summary>Busca o site do tenant, quando existir.</summary>
    Task<Site?> FindSiteAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Registra o site do tenant.</summary>
    Task AddSiteAsync(Site site, CancellationToken cancellationToken = default);

    /// <summary>Registra o arquivo enviado pelo proprietário.</summary>
    Task AddAssetAsync(SiteAsset asset, CancellationToken cancellationToken = default);

    /// <summary>Persiste as alterações pendentes.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
