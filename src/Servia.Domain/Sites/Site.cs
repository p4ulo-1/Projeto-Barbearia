using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Sites;

/// <summary>
/// Site público de um estabelecimento.
///
/// Agregado raiz do contexto de site: agrupa páginas, tema e versões
/// publicadas. O <c>Subdomain</c> é o endereço do site na plataforma (F3.3).
///
/// <b>Publicação com aprovação (F3.6):</b> o site tem um rascunho editável e
/// uma versão publicada. Alterações no rascunho não afetam o site no ar até
/// que <see cref="Publish"/> seja chamado explicitamente.
///
/// <b>Rollback (F3.7):</b> cada publicação gera uma versão imutável; é
/// possível voltar a qualquer versão anterior.
/// </summary>
public sealed class Site : ITenantScoped
{
    private readonly List<SitePage> _pages = [];
    private readonly List<SitePublication> _publications = [];

    private Site(Guid id, Guid tenantId, string subdomain, SiteTheme theme)
    {
        Id = id;
        TenantId = tenantId;
        Subdomain = subdomain;
        Theme = theme;
        Status = SiteStatus.Draft;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>.
    /// </summary>
    private Site()
    {
        Subdomain = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>Endereço do site na plataforma: <c>{subdomain}.plataforma.com</c>.</summary>
    public string Subdomain { get; private set; }

    /// <summary>Domínio próprio, quando configurado. Fora do MVP (D10).</summary>
    public string? CustomDomain { get; private set; }

    public SiteTheme Theme { get; private set; }

    public SiteStatus Status { get; private set; }

    /// <summary>Versão atualmente publicada, quando houver.</summary>
    public Guid? PublishedVersionId { get; private set; }

    public IReadOnlyCollection<SitePage> Pages => _pages;

    public IReadOnlyCollection<SitePublication> Publications => _publications;

    /// <summary>Indica se o site tem uma versão no ar.</summary>
    public bool IsPublished => Status == SiteStatus.Published && PublishedVersionId.HasValue;

    public static Site Create(Guid tenantId, string subdomain)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(subdomain))
        {
            throw new DomainRuleException("O subdomínio é obrigatório.");
        }

        var normalized = subdomain.Trim().ToLowerInvariant();

        if (!IsValidSubdomain(normalized))
        {
            throw new DomainRuleException(
                "O subdomínio deve conter apenas letras minúsculas, números e hífen, com 3 a 40 caracteres.");
        }

        var site = new Site(Guid.NewGuid(), tenantId, normalized, SiteTheme.Default);

        // Todo site nasce com a página inicial.
        site._pages.Add(SitePage.Create(string.Empty, "Início"));

        return site;
    }

    /// <summary>Adiciona uma página ao site.</summary>
    public SitePage AddPage(string slug, string title)
    {
        if (_pages.Any(page => string.Equals(page.Slug, slug.Trim().ToLowerInvariant(), StringComparison.Ordinal)))
        {
            throw new DomainRuleException($"Já existe uma página com o slug '{slug}'.");
        }

        var page = SitePage.Create(slug, title, _pages.Count);
        _pages.Add(page);
        return page;
    }

    /// <summary>Remove uma página do site. A página inicial não pode ser removida.</summary>
    public void RemovePage(Guid pageId)
    {
        var page = _pages.FirstOrDefault(item => item.Id == pageId);

        if (page is null)
        {
            throw new DomainRuleException("A página não pertence a este site.");
        }

        if (string.IsNullOrEmpty(page.Slug))
        {
            throw new DomainRuleException("A página inicial não pode ser removida.");
        }

        _pages.Remove(page);
    }

    /// <summary>Altera o tema do site.</summary>
    public void ChangeTheme(SiteTheme theme) => Theme = theme;

    /// <summary>
    /// Publica o rascunho atual, gerando uma nova versão imutável (F3.6).
    /// </summary>
    public SitePublication Publish(Guid publishedBy, DateTimeOffset now)
    {
        if (publishedBy == Guid.Empty)
        {
            throw new DomainRuleException("O autor da publicação é obrigatório.");
        }

        if (_pages.Count == 0)
        {
            throw new DomainRuleException("O site precisa ter ao menos uma página para ser publicado.");
        }

        var incomplete = _pages.FirstOrDefault(page => !page.IsComplete());

        if (incomplete is not null)
        {
            throw new DomainRuleException(
                $"A página '{incomplete.Title}' tem seções incompletas e não pode ser publicada.");
        }

        var version = _publications.Count + 1;
        var publication = SitePublication.Create(Id, version, BuildSnapshot(), publishedBy, now);

        _publications.Add(publication);
        PublishedVersionId = publication.Id;
        Status = SiteStatus.Published;

        return publication;
    }

    /// <summary>
    /// Restaura uma versão anterior (F3.7).
    /// A versão restaurada torna-se a publicação corrente.
    /// </summary>
    public void RollbackTo(Guid publicationId)
    {
        var publication = _publications.FirstOrDefault(item => item.Id == publicationId);

        if (publication is null)
        {
            throw new DomainRuleException("A versão informada não pertence a este site.");
        }

        PublishedVersionId = publication.Id;
        Status = SiteStatus.Published;
    }

    /// <summary>Retira o site do ar, mantendo o rascunho.</summary>
    public void Unpublish()
    {
        if (!IsPublished)
        {
            throw new DomainRuleException("O site não está publicado.");
        }

        Status = SiteStatus.Draft;
    }

    /// <summary>
    /// Gera o snapshot do rascunho atual, usado na publicação e na prévia.
    /// </summary>
    public string BuildSnapshot()
    {
        var pages = _pages.Select(page => new
        {
            slug = page.Slug,
            title = page.Title,
            order = page.SortOrder,
            sections = page.Sections.Select(section => new
            {
                type = section.ComponentType.ToString(),
                order = section.SortOrder,
                configuration = section.Configuration
            })
        });

        var snapshot = new
        {
            subdomain = Subdomain,
            theme = new
            {
                primary = Theme.PrimaryColor,
                secondary = Theme.SecondaryColor,
                font = Theme.FontFamily,
                layout = Theme.LayoutStyle
            },
            pages
        };

        return System.Text.Json.JsonSerializer.Serialize(snapshot);
    }

    private static bool IsValidSubdomain(string value)
    {
        if (value.Length is < 3 or > 40)
        {
            return false;
        }

        if (value[0] == '-' || value[^1] == '-')
        {
            return false;
        }

        return value.All(character =>
            char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '-');
    }
}

/// <summary>Estado do site.</summary>
public enum SiteStatus
{
    /// <summary>Rascunho: ainda não publicado ou retirado do ar.</summary>
    Draft = 1,

    /// <summary>Publicado e acessível.</summary>
    Published = 2
}
