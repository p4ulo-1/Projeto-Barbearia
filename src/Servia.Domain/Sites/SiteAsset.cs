using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Sites;

/// <summary>
/// Arquivo enviado pelo proprietário para compor o site (item 1.3).
///
/// <b>Não é informação validada por ter sido enviada.</b> O arquivo entra
/// como pendente e só é usado na configuração depois de aprovado. Um
/// logotipo enviado por engano não deve ir ao ar.
///
/// <b>Isolamento:</b> todo arquivo pertence a um tenant. O caminho de
/// armazenamento inclui o tenant, e a consulta é filtrada pelo contexto —
/// um estabelecimento nunca alcança o arquivo de outro.
/// </summary>
public sealed class SiteAsset : ITenantScoped
{
    /// <summary>Tamanho máximo aceito: 5 MB.</summary>
    public const long MaxSizeBytes = 5 * 1024 * 1024;

    /// <summary>
    /// Dimensão máxima (largura ou altura), em pixels (C4). Imagens maiores
    /// são rejeitadas para evitar consumo desproporcional ao decodificar.
    /// </summary>
    public const int MaxDimensionPixels = 8000;

    /// <summary>
    /// Formatos aceitos (C4): apenas PNG e JPEG. SVG, GIF e WebP são
    /// rejeitados — SVG pode carregar script e os demais não são necessários
    /// para o site.
    /// </summary>
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg"
        };

    /// <summary>MIME types aceitos, por extensão (C4).</summary>
    private static readonly IReadOnlyDictionary<string, string[]> AllowedContentTypes =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = ["image/png"],
            [".jpg"] = ["image/jpeg"],
            [".jpeg"] = ["image/jpeg"]
        };

    /// <summary>Orientação ao cliente sobre os formatos aceitos (C4).</summary>
    public const string AcceptedFormatsMessage =
        "A plataforma aceita imagens PNG e JPEG. "
        + "Recomendamos imagens de boa resolução para preservar a qualidade visual do site.";

    private SiteAsset(
        Guid id,
        Guid tenantId,
        Guid? onboardingSessionId,
        SiteAssetKind kind,
        string fileName,
        string contentType,
        long sizeInBytes,
        string storagePath,
        string checksum,
        DateTimeOffset uploadedAt)
    {
        Id = id;
        TenantId = tenantId;
        OnboardingSessionId = onboardingSessionId;
        Kind = kind;
        FileName = fileName;
        ContentType = contentType;
        SizeInBytes = sizeInBytes;
        StoragePath = storagePath;
        Checksum = checksum;
        UploadedAt = uploadedAt;
        Status = SiteAssetStatus.Pending;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Register"/>.
    /// </summary>
    private SiteAsset()
    {
        FileName = string.Empty;
        ContentType = string.Empty;
        StoragePath = string.Empty;
        Checksum = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>Sessão de onboarding que originou o envio, quando houver.</summary>
    public Guid? OnboardingSessionId { get; private set; }

    public SiteAssetKind Kind { get; private set; }

    public string FileName { get; private set; }

    public string ContentType { get; private set; }

    public long SizeInBytes { get; private set; }

    /// <summary>Caminho relativo no armazenamento, sempre prefixado pelo tenant.</summary>
    public string StoragePath { get; private set; }

    /// <summary>Hash do conteúdo, para verificação de integridade.</summary>
    public string Checksum { get; private set; }

    public SiteAssetStatus Status { get; private set; }

    public DateTimeOffset UploadedAt { get; private set; }

    /// <summary>Indica se o arquivo pode ser usado na configuração do site.</summary>
    public bool IsUsable => Status == SiteAssetStatus.Approved;

    /// <summary>
    /// Registra um arquivo enviado.
    ///
    /// Valida extensão, tamanho e nome. O conteúdo binário é validado pelo
    /// armazenamento, que verifica a assinatura do arquivo — extensão
    /// sozinha não prova que o conteúdo corresponde.
    /// </summary>
    public static SiteAsset Register(
        Guid tenantId,
        Guid? onboardingSessionId,
        SiteAssetKind kind,
        string fileName,
        string contentType,
        long sizeInBytes,
        string storagePath,
        string checksum,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new DomainRuleException("O nome do arquivo é obrigatório.");
        }

        // O nome vem do cliente: normaliza para impedir travessia de caminho.
        var safeName = Path.GetFileName(fileName.Trim());

        if (string.IsNullOrWhiteSpace(safeName) || safeName.Contains("..", StringComparison.Ordinal))
        {
            throw new DomainRuleException("O nome do arquivo é inválido.");
        }

        var extension = Path.GetExtension(safeName);

        if (!AllowedExtensions.Contains(extension))
        {
            throw new DomainRuleException(
                $"O formato '{extension}' não é aceito. {AcceptedFormatsMessage}");
        }

        // C4: o MIME informado precisa ser compatível com a extensão. Um
        // arquivo .png declarado como image/svg+xml é rejeitado.
        var normalizedContentType = string.IsNullOrWhiteSpace(contentType)
            ? string.Empty
            : contentType.Trim().ToLowerInvariant();

        if (normalizedContentType.Length > 0
            && AllowedContentTypes.TryGetValue(extension, out var allowedTypes)
            && !allowedTypes.Contains(normalizedContentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new DomainRuleException(
                $"O tipo '{contentType}' não corresponde ao formato '{extension}'. {AcceptedFormatsMessage}");
        }

        if (sizeInBytes <= 0)
        {
            throw new DomainRuleException("O arquivo está vazio.");
        }

        if (sizeInBytes > MaxSizeBytes)
        {
            throw new DomainRuleException(
                $"O arquivo excede o limite de {MaxSizeBytes / (1024 * 1024)} MB.");
        }

        if (string.IsNullOrWhiteSpace(storagePath))
        {
            throw new DomainRuleException("O caminho de armazenamento é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(checksum))
        {
            throw new DomainRuleException("O checksum é obrigatório para verificação de integridade.");
        }

        return new SiteAsset(
            Guid.NewGuid(), tenantId, onboardingSessionId, kind, safeName,
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim(),
            sizeInBytes, storagePath.Trim(), checksum.Trim(), now);
    }

    /// <summary>Marca o arquivo como aprovado para uso.</summary>
    public void Approve()
    {
        if (Status == SiteAssetStatus.Rejected)
        {
            throw new DomainRuleException("Um arquivo rejeitado não pode ser aprovado.");
        }

        Status = SiteAssetStatus.Approved;
    }

    /// <summary>Rejeita o arquivo, com motivo.</summary>
    public void Reject(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainRuleException("O motivo da rejeição é obrigatório.");
        }

        Status = SiteAssetStatus.Rejected;
        RejectionReason = reason.Trim();
    }

    public string? RejectionReason { get; private set; }
}

/// <summary>Finalidade do arquivo no site.</summary>
public enum SiteAssetKind
{
    /// <summary>Logotipo do estabelecimento.</summary>
    Logo = 1,

    /// <summary>Foto do estabelecimento.</summary>
    EstablishmentPhoto = 2,

    /// <summary>Foto de profissional.</summary>
    ProfessionalPhoto = 3,

    /// <summary>Imagem de serviço ou produto.</summary>
    ServiceImage = 4,

    /// <summary>Imagem de apoio (banner, fundo).</summary>
    SupportingImage = 5
}

/// <summary>Estado do arquivo.</summary>
public enum SiteAssetStatus
{
    /// <summary>Recebido, ainda não validado.</summary>
    Pending = 1,

    /// <summary>Aprovado para uso na configuração.</summary>
    Approved = 2,

    /// <summary>Rejeitado, não pode ser usado.</summary>
    Rejected = 3
}
