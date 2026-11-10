using Servia.Domain.Common;
using Servia.Domain.Sites;

namespace Servia.Domain.Ai;

/// <summary>
/// Proposta de site gerada pela IA (F8.3, F8.6).
///
/// É um rascunho: só entra no ar após aprovação explícita (F3.6).
///
/// <b>Guardrail (F8.6):</b> a proposta só pode conter componentes da
/// biblioteca controlada e propriedades previstas no schema. Uma proposta
/// com componente ou propriedade desconhecida é rejeitada na construção —
/// a IA não consegue introduzir código arbitrário.
/// </summary>
public sealed class AiChangeSet
{
    private readonly List<AiSectionProposal> _sections = [];

    private AiChangeSet(Guid id, string summary, SiteTheme theme)
    {
        Id = id;
        Summary = summary;
        Theme = theme;
    }

    public Guid Id { get; private set; }

    /// <summary>Resumo legível da proposta, para o usuário revisar.</summary>
    public string Summary { get; private set; }

    public SiteTheme Theme { get; private set; }

    public IReadOnlyCollection<AiSectionProposal> Sections => _sections;

    public static AiChangeSet Create(string summary, SiteTheme theme)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new DomainRuleException("O resumo da proposta é obrigatório.");
        }

        return new AiChangeSet(Guid.NewGuid(), summary.Trim(), theme);
    }

    /// <summary>
    /// Adiciona uma seção à proposta, validando o componente e as
    /// propriedades contra o schema (F8.6).
    /// </summary>
    public AiSectionProposal AddSection(
        SiteComponentType componentType,
        IReadOnlyDictionary<string, string> configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (!SiteComponentSchema.IsSupported(componentType))
        {
            throw new DomainRuleException(
                $"O componente {componentType} não faz parte da biblioteca controlada.");
        }

        var allowed = SiteComponentSchema.AllowedKeys(componentType);

        foreach (var key in configuration.Keys)
        {
            if (!allowed.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                throw new DomainRuleException(
                    $"A propriedade '{key}' não é válida para o componente {componentType}.");
            }
        }

        var section = new AiSectionProposal(componentType, configuration, _sections.Count);
        _sections.Add(section);

        return section;
    }

    /// <summary>
    /// Indica se a proposta tem todas as seções completas e pode ser
    /// aplicada ao site.
    /// </summary>
    public bool IsComplete()
    {
        if (_sections.Count == 0)
        {
            return false;
        }

        return _sections.All(section =>
        {
            var required = SiteComponentSchema.RequiredKeys(section.ComponentType);

            return required.All(key =>
                section.Configuration.TryGetValue(key, out var value)
                && !string.IsNullOrWhiteSpace(value));
        });
    }
}

/// <summary>Seção proposta pela IA.</summary>
public sealed class AiSectionProposal
{
    internal AiSectionProposal(
        SiteComponentType componentType,
        IReadOnlyDictionary<string, string> configuration,
        int sortOrder)
    {
        ComponentType = componentType;
        Configuration = configuration;
        SortOrder = sortOrder;
    }

    public SiteComponentType ComponentType { get; }

    public IReadOnlyDictionary<string, string> Configuration { get; }

    public int SortOrder { get; }
}
