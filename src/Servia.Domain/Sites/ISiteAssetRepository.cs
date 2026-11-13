namespace Servia.Domain.Sites;

/// <summary>
/// Contrato de persistência de arquivos do site (C11).
///
/// Separado de <see cref="IOnboardingRepository"/> porque o serviço de
/// arquivos não precisa conhecer sessões, propostas ou sites — só os
/// próprios arquivos. Isso reduz o acoplamento e facilita testar o serviço.
///
/// <b>Isolamento:</b> todas as operações recebem o <c>tenantId</c> explícito,
/// além do filtro global. Um repositório nunca atravessa estabelecimentos.
/// </summary>
public interface ISiteAssetRepository
{
    /// <summary>Busca um arquivo do tenant pelo identificador.</summary>
    Task<SiteAsset?> FindAsync(
        Guid tenantId,
        Guid assetId,
        CancellationToken cancellationToken = default);

    /// <summary>Lista os arquivos de uma sessão de onboarding do tenant.</summary>
    Task<IReadOnlyList<SiteAsset>> ListBySessionAsync(
        Guid tenantId,
        Guid onboardingSessionId,
        CancellationToken cancellationToken = default);

    /// <summary>Registra um arquivo.</summary>
    Task AddAsync(SiteAsset asset, CancellationToken cancellationToken = default);

    /// <summary>Persiste as alterações pendentes.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
