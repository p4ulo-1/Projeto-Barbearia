using Servia.Domain.Common;

namespace Servia.Domain.Sites;

/// <summary>
/// Página do site: agrupa seções em uma ordem de exibição.
///
/// O <c>Slug</c> identifica a página na URL. A página inicial usa o slug
/// vazio, que corresponde à raiz do site.
/// </summary>
public sealed class SitePage
{
    private readonly List<SiteSection> _sections = [];

    private SitePage(Guid id, string slug, string title, int sortOrder)
    {
        Id = id;
        Slug = slug;
        Title = title;
        SortOrder = sortOrder;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>.
    /// </summary>
    private SitePage()
    {
        Slug = string.Empty;
        Title = string.Empty;
    }

    public Guid Id { get; private set; }

    /// <summary>Identificador da página na URL. Vazio para a página inicial.</summary>
    public string Slug { get; private set; }

    public string Title { get; private set; }

    public int SortOrder { get; private set; }

    public IReadOnlyCollection<SiteSection> Sections => _sections;

    public static SitePage Create(string slug, string title, int sortOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainRuleException("O título da página é obrigatório.");
        }

        if (sortOrder < 0)
        {
            throw new DomainRuleException("A ordem da página não pode ser negativa.");
        }

        return new SitePage(Guid.NewGuid(), NormalizeSlug(slug), title.Trim(), sortOrder);
    }

    /// <summary>Adiciona uma seção ao final da página.</summary>
    public SiteSection AddSection(SiteComponentType componentType)
    {
        var section = SiteSection.Create(componentType, _sections.Count);
        _sections.Add(section);
        return section;
    }

    /// <summary>Remove uma seção da página.</summary>
    public void RemoveSection(Guid sectionId)
    {
        var section = _sections.FirstOrDefault(item => item.Id == sectionId);

        if (section is null)
        {
            throw new DomainRuleException("A seção não pertence a esta página.");
        }

        _sections.Remove(section);
        Reorder();
    }

    /// <summary>
    /// Move uma seção para a posição informada, ajustando as demais.
    /// </summary>
    public void MoveSection(Guid sectionId, int newPosition)
    {
        var section = _sections.FirstOrDefault(item => item.Id == sectionId);

        if (section is null)
        {
            throw new DomainRuleException("A seção não pertence a esta página.");
        }

        if (newPosition < 0 || newPosition >= _sections.Count)
        {
            throw new DomainRuleException("A posição informada está fora do intervalo.");
        }

        _sections.Remove(section);
        _sections.Insert(newPosition, section);
        Reorder();
    }

    /// <summary>Renomeia a página.</summary>
    public void Rename(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainRuleException("O título da página é obrigatório.");
        }

        Title = title.Trim();
    }

    /// <summary>Indica se todas as seções estão completas.</summary>
    public bool IsComplete() => _sections.All(section => section.IsComplete());

    private void Reorder()
    {
        for (var index = 0; index < _sections.Count; index++)
        {
            _sections[index].MoveTo(index);
        }
    }

    private static string NormalizeSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return string.Empty;
        }

        return slug.Trim().ToLowerInvariant();
    }
}
