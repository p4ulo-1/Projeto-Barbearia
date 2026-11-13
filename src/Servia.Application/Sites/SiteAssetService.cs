using Servia.Domain.Common;
using Servia.Domain.Sites;

namespace Servia.Application.Sites;

/// <summary>
/// Recebimento e gestão de arquivos do site (item 1.3).
///
/// <b>Validação em duas camadas:</b> <see cref="SiteAsset.Register"/> valida
/// nome, extensão, MIME e tamanho; <see cref="ISiteAssetStorage.StoreAsync"/>
/// valida a assinatura binária, a decodificação e as dimensões do conteúdo.
///
/// <b>Arquivo não é informação validada:</b> todo arquivo entra como
/// <c>Pending</c> e só é usado na configuração após aprovação explícita.
///
/// <b>Isolamento:</b> o arquivo é gravado sob a pasta do tenant e a consulta
/// é filtrada pelo contexto. Um estabelecimento nunca acessa o arquivo de
/// outro — nem pelo identificador.
///
/// <b>C11:</b> depende apenas de <see cref="ISiteAssetRepository"/>, não do
/// repositório de onboarding. A verificação de posse da sessão (C5) é feita
/// pelo chamador, que já conhece a sessão.
/// </summary>
public sealed class SiteAssetService(
    ISiteAssetRepository repository,
    ISiteAssetStorage storage,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Recebe um arquivo, valida e armazena em estado pendente.
    /// </summary>
    public async Task<SiteAsset> UploadAsync(
        Guid tenantId,
        Guid? onboardingSessionId,
        SiteAssetKind kind,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        // Armazena primeiro: o conteudo e inspecionado antes de qualquer
        // registro. Se for invalido, nada e persistido.
        var stored = await storage.StoreAsync(tenantId, fileName, contentType, content, cancellationToken);

        var asset = SiteAsset.Register(
            tenantId,
            onboardingSessionId,
            kind,
            fileName,
            stored.DetectedContentType,
            stored.SizeInBytes,
            stored.StoragePath,
            stored.Checksum,
            timeProvider.GetUtcNow());

        await repository.AddAsync(asset, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return asset;
    }

    /// <summary>
    /// Abre o arquivo para download, sempre escopado ao tenant.
    /// </summary>
    public Task<Stream> OpenAsync(
        Guid tenantId,
        SiteAsset asset,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asset);

        if (asset.TenantId != tenantId)
        {
            // Fronteira de confianca: um arquivo de outro tenant e "nao existe".
            throw NotFoundException.For("Arquivo", asset.Id);
        }

        return storage.OpenReadAsync(tenantId, asset.StoragePath, cancellationToken);
    }
}
