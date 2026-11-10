using Servia.Domain.Common;

namespace Servia.Domain.Sites;

/// <summary>
/// Versão publicada do site (F3.6, F3.7).
///
/// É imutável: uma vez criada, a versão não muda. Isso é o que torna o
/// rollback confiável — restaurar uma versão antiga devolve exatamente o que
/// estava no ar naquele momento.
///
/// O snapshot guarda o estado completo do site em JSON, independente do
/// rascunho atual.
/// </summary>
public sealed class SitePublication
{
    private SitePublication(
        Guid id,
        Guid siteId,
        int version,
        string snapshot,
        Guid publishedBy,
        DateTimeOffset publishedAt)
    {
        Id = id;
        SiteId = siteId;
        Version = version;
        Snapshot = snapshot;
        PublishedBy = publishedBy;
        PublishedAt = publishedAt;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>.
    /// </summary>
    private SitePublication()
    {
        Snapshot = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid SiteId { get; private set; }

    /// <summary>Número sequencial da versão, começando em 1.</summary>
    public int Version { get; private set; }

    /// <summary>Estado completo do site no momento da publicação.</summary>
    public string Snapshot { get; private set; }

    public Guid PublishedBy { get; private set; }

    public DateTimeOffset PublishedAt { get; private set; }

    public static SitePublication Create(
        Guid siteId,
        int version,
        string snapshot,
        Guid publishedBy,
        DateTimeOffset now)
    {
        if (siteId == Guid.Empty)
        {
            throw new DomainRuleException("O site é obrigatório.");
        }

        if (version <= 0)
        {
            throw new DomainRuleException("O número da versão deve ser maior que zero.");
        }

        if (string.IsNullOrWhiteSpace(snapshot))
        {
            throw new DomainRuleException("O snapshot da publicação é obrigatório.");
        }

        if (publishedBy == Guid.Empty)
        {
            throw new DomainRuleException("O autor da publicação é obrigatório.");
        }

        return new SitePublication(Guid.NewGuid(), siteId, version, snapshot, publishedBy, now);
    }
}
