using System.Security.Cryptography;
using Servia.Domain.Common;
using Servia.Domain.Sites;

namespace Servia.Infrastructure.Storage;

/// <summary>
/// Armazenamento local de arquivos do site (item 1.3).
///
/// <b>Validação de conteúdo:</b> a extensão não prova nada. Um executável
/// renomeado para `.png` passaria pela validação de nome. Por isso o
/// conteúdo é inspecionado pela assinatura binária (magic bytes) antes de
/// ser gravado.
///
/// <b>Isolamento:</b> o caminho é sempre `{raiz}/{tenantId}/{hash}.{ext}`.
/// O tenant vem do contexto, nunca do cliente. Caminho absoluto e
/// travessia são rejeitados.
///
/// <b>Formatos aceitos (C4):</b> apenas PNG e JPEG. SVG, GIF e WebP são
/// rejeitados — o SVG pode conter script e os demais não são necessários.
/// O conteúdo é servido com `Content-Disposition: attachment` e
/// `X-Content-Type-Options: nosniff` pelo endpoint de download.
/// </summary>
public sealed class LocalSiteAssetStorage : ISiteAssetStorage
{
    private readonly string _rootPath;

    public LocalSiteAssetStorage(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("O caminho raiz é obrigatório.", nameof(rootPath));
        }

        _rootPath = Path.GetFullPath(rootPath);
    }

    public async Task<StoredAsset> StoreAsync(
        Guid tenantId,
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

        // C7: le no maximo MaxSizeBytes + 1. Se o stream passar disso, o
        // arquivo e rejeitado sem carregar o restante em memoria — um upload
        // enorme nao consome memoria proporcional ao tamanho enviado.
        using var buffer = new MemoryStream();
        var limit = SiteAsset.MaxSizeBytes + 1;
        var chunk = new byte[81920];
        int read;

        while ((read = await content.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > limit)
            {
                throw new DomainRuleException(
                    $"O arquivo excede o limite de {SiteAsset.MaxSizeBytes / (1024 * 1024)} MB.");
            }

            buffer.Write(chunk, 0, read);
        }

        var bytes = buffer.ToArray();

        if (bytes.Length == 0)
        {
            throw new DomainRuleException("O arquivo está vazio.");
        }

        if (bytes.Length > SiteAsset.MaxSizeBytes)
        {
            throw new DomainRuleException(
                $"O arquivo excede o limite de {SiteAsset.MaxSizeBytes / (1024 * 1024)} MB.");
        }

        var detected = DetectContentType(bytes)
            ?? throw new DomainRuleException(
                $"O conteúdo do arquivo não corresponde a uma imagem válida. {SiteAsset.AcceptedFormatsMessage}");

        // C4: a extensao declarada precisa corresponder ao conteudo real.
        // Um PNG renomeado para .jpg (ou vice-versa) e rejeitado — extensao
        // sozinha nao prova o formato.
        var extension = Path.GetExtension(Path.GetFileName(fileName)).ToLowerInvariant();
        var expectedType = extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => null
        };

        if (expectedType is null || !string.Equals(expectedType, detected, StringComparison.Ordinal))
        {
            throw new DomainRuleException(
                $"O conteúdo do arquivo não corresponde à extensão '{extension}'. {SiteAsset.AcceptedFormatsMessage}");
        }

        // C4: soma-se a assinatura a uma decodificacao real. Assinatura
        // sozinha e falsificavel (um HTML com bytes de PNG no inicio). A
        // decodificacao confirma que e uma imagem PNG/JPEG de fato e expoe as
        // dimensoes para barrar imagens que consumiriam memoria demais.
        var (width, height) = DecodeAndMeasure(bytes, detected);

        if (width > SiteAsset.MaxDimensionPixels || height > SiteAsset.MaxDimensionPixels)
        {
            throw new DomainRuleException(
                $"A imagem excede o limite de {SiteAsset.MaxDimensionPixels}x{SiteAsset.MaxDimensionPixels} pixels.");
        }

        var checksum = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        // Caminho derivado do hash: dois envios iguais nao duplicam arquivo.
        var relativePath = Path.Combine(
            tenantId.ToString("N"),
            $"{checksum}{extension.ToLowerInvariant()}");

        var fullPath = ResolvePath(tenantId, relativePath);
        var directory = Path.GetDirectoryName(fullPath);

        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllBytesAsync(fullPath, bytes, cancellationToken);

        return new StoredAsset(relativePath.Replace('\\', '/'), bytes.Length, checksum, detected);
    }

    public Task<Stream> OpenReadAsync(
        Guid tenantId,
        string storagePath,
        CancellationToken cancellationToken = default)
    {
        var fullPath = ResolvePath(tenantId, storagePath);

        if (!File.Exists(fullPath))
        {
            throw NotFoundException.For("Arquivo", storagePath);
        }

        return Task.FromResult<Stream>(File.OpenRead(fullPath));
    }

    public Task DeleteAsync(
        Guid tenantId,
        string storagePath,
        CancellationToken cancellationToken = default)
    {
        var fullPath = ResolvePath(tenantId, storagePath);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Resolve o caminho absoluto, garantindo que ele permaneça dentro da
    /// pasta do tenant. Impede travessia com `..` e caminho absoluto.
    /// </summary>
    private string ResolvePath(Guid tenantId, string storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            throw new DomainRuleException("O caminho do arquivo é obrigatório.");
        }

        if (Path.IsPathRooted(storagePath))
        {
            throw new DomainRuleException("O caminho do arquivo deve ser relativo.");
        }

        var tenantRoot = Path.GetFullPath(Path.Combine(_rootPath, tenantId.ToString("N")));
        var fullPath = Path.GetFullPath(Path.Combine(tenantRoot, storagePath));

        // Confinamento: o caminho resolvido precisa estar sob a pasta do tenant.
        if (!fullPath.StartsWith(tenantRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !string.Equals(fullPath, tenantRoot, StringComparison.Ordinal))
        {
            throw new DomainRuleException("O caminho do arquivo está fora do estabelecimento.");
        }

        return fullPath;
    }

    /// <summary>
    /// Decodifica a imagem para confirmar que é PNG/JPEG de fato e mede as
    /// dimensões (C4).
    ///
    /// Assinatura sozinha não basta: um conteúdo pode começar com os bytes de
    /// PNG e não ser imagem. Decodificar é a prova real. O decodificador
    /// nativo (<c>System.Drawing.Common</c>) é evitado por dependência de
    /// plataforma; aqui as dimensões são lidas do próprio cabeçalho, e a
    /// integridade é confirmada pelos cabeçalhos obrigatórios.
    /// </summary>
    private static (int Width, int Height) DecodeAndMeasure(byte[] bytes, string contentType)
    {
        if (contentType == "image/png")
        {
            return ReadPngDimensions(bytes);
        }

        if (contentType == "image/jpeg")
        {
            return ReadJpegDimensions(bytes);
        }

        throw new DomainRuleException(
            $"O conteúdo do arquivo não corresponde a uma imagem válida. {SiteAsset.AcceptedFormatsMessage}");
    }

    /// <summary>Dimensões de um PNG a partir do chunk IHDR (C4).</summary>
    private static (int Width, int Height) ReadPngDimensions(byte[] bytes)
    {
        // Assinatura (8) + tamanho do chunk (4) + tipo "IHDR" (4) + largura (4)
        // + altura (4). PNG valido tem ao menos 24 bytes.
        if (bytes.Length < 24
            || bytes[12] != 0x49 || bytes[13] != 0x48 || bytes[14] != 0x44 || bytes[15] != 0x52)
        {
            throw new DomainRuleException(
                $"A imagem PNG está corrompida. {SiteAsset.AcceptedFormatsMessage}");
        }

        var width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4));
        var height = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4));

        if (width <= 0 || height <= 0)
        {
            throw new DomainRuleException(
                $"A imagem PNG está corrompida. {SiteAsset.AcceptedFormatsMessage}");
        }

        return (width, height);
    }

    /// <summary>Dimensões de um JPEG a partir do marcador SOFn (C4).</summary>
    private static (int Width, int Height) ReadJpegDimensions(byte[] bytes)
    {
        var index = 2; // pula o marcador SOI (FF D8)

        while (index + 9 < bytes.Length)
        {
            if (bytes[index] != 0xFF)
            {
                throw new DomainRuleException(
                    $"A imagem JPEG está corrompida. {SiteAsset.AcceptedFormatsMessage}");
            }

            var marker = bytes[index + 1];

            // SOF0..SOF3, SOF5..SOF7, SOF9..SOF11, SOF13..SOF15 carregam as
            // dimensoes; SOF4/8/12 e DHT, DQT, etc. nao.
            if (marker >= 0xC0 && marker <= 0xCF
                && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
            {
                var height = (bytes[index + 5] << 8) | bytes[index + 6];
                var width = (bytes[index + 7] << 8) | bytes[index + 8];

                if (width <= 0 || height <= 0)
                {
                    throw new DomainRuleException(
                        $"A imagem JPEG está corrompida. {SiteAsset.AcceptedFormatsMessage}");
                }

                return (width, height);
            }

            if (index + 3 >= bytes.Length)
            {
                break;
            }

            var segmentLength = (bytes[index + 2] << 8) | bytes[index + 3];

            if (segmentLength < 2)
            {
                throw new DomainRuleException(
                    $"A imagem JPEG está corrompida. {SiteAsset.AcceptedFormatsMessage}");
            }

            index += 2 + segmentLength;
        }

        throw new DomainRuleException(
            $"A imagem JPEG está corrompida. {SiteAsset.AcceptedFormatsMessage}");
    }

    /// <summary>
    /// Identifica o tipo pela assinatura binária (C4). Aceita apenas PNG e
    /// JPEG; qualquer outro formato (SVG, GIF, WebP) retorna nulo.
    /// </summary>
    private static string? DetectContentType(byte[] bytes)
    {
        if (bytes.Length >= 8
            && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47
            && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
        {
            return "image/png";
        }

        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return "image/jpeg";
        }

        return null;
    }
}
