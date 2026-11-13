namespace Servia.Domain.Sites;

/// <summary>
/// Contrato de armazenamento de arquivos do site (item 1.3).
///
/// <b>Por que no domínio:</b> a infraestrutura implementa sem depender da
/// aplicação (ADR-004). O domínio define o que precisa; o disco, o S3 ou
/// o Azure Blob decidem como.
///
/// <b>Isolamento:</b> todo caminho é prefixado pelo tenant. A
/// implementação não aceita caminho absoluto nem travessia.
/// </summary>
public interface ISiteAssetStorage
{
    /// <summary>
    /// Armazena o conteúdo e devolve o caminho relativo.
    ///
    /// Valida a assinatura binária do arquivo: extensão sozinha não prova
    /// que o conteúdo corresponde. Um executável renomeado para `.png` é
    /// rejeitado aqui.
    /// </summary>
    Task<StoredAsset> StoreAsync(
        Guid tenantId,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken = default);

    /// <summary>Abre o arquivo para leitura.</summary>
    Task<Stream> OpenReadAsync(
        Guid tenantId,
        string storagePath,
        CancellationToken cancellationToken = default);

    /// <summary>Remove o arquivo do armazenamento.</summary>
    Task DeleteAsync(
        Guid tenantId,
        string storagePath,
        CancellationToken cancellationToken = default);
}

/// <summary>Resultado do armazenamento.</summary>
public sealed record StoredAsset(
    string StoragePath,
    long SizeInBytes,
    string Checksum,
    string DetectedContentType);
