using Servia.Domain.Common;

namespace Servia.Domain.Sites;

/// <summary>
/// Seção de uma página do site: um componente configurado.
///
/// A configuração é um dicionário de chave-valor validado contra o schema do
/// componente. A IA propõe a configuração; o domínio garante que ela é
/// compatível com o componente escolhido (ADR-007).
/// </summary>
public sealed class SiteSection
{
    private readonly Dictionary<string, string> _configuration = [];

    private SiteSection(Guid id, SiteComponentType componentType, int sortOrder)
    {
        Id = id;
        ComponentType = componentType;
        SortOrder = sortOrder;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>.
    /// </summary>
    private SiteSection()
    {
    }

    public Guid Id { get; private set; }

    public SiteComponentType ComponentType { get; private set; }

    public int SortOrder { get; private set; }

    public IReadOnlyDictionary<string, string> Configuration => _configuration;

    public static SiteSection Create(SiteComponentType componentType, int sortOrder = 0)
    {
        if (!Enum.IsDefined(componentType))
        {
            throw new DomainRuleException("O tipo de componente é inválido.");
        }

        if (sortOrder < 0)
        {
            throw new DomainRuleException("A ordem da seção não pode ser negativa.");
        }

        return new SiteSection(Guid.NewGuid(), componentType, sortOrder);
    }

    /// <summary>
    /// Define uma propriedade de configuração.
    /// A chave precisa ser uma propriedade conhecida do componente.
    /// </summary>
    public void SetProperty(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new DomainRuleException("A chave da configuração é obrigatória.");
        }

        var allowed = SiteComponentSchema.AllowedKeys(ComponentType);

        if (!allowed.Contains(key.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            throw new DomainRuleException(
                $"A propriedade '{key}' não é válida para o componente {ComponentType}.");
        }

        _configuration[key.Trim()] = value ?? string.Empty;
    }

    /// <summary>Remove uma propriedade de configuração.</summary>
    public void RemoveProperty(string key)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            _configuration.Remove(key.Trim());
        }
    }

    /// <summary>Altera a ordem da seção na página.</summary>
    public void MoveTo(int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new DomainRuleException("A ordem da seção não pode ser negativa.");
        }

        SortOrder = sortOrder;
    }

    /// <summary>
    /// Indica se a seção tem todas as propriedades obrigatórias preenchidas.
    /// </summary>
    public bool IsComplete()
    {
        var required = SiteComponentSchema.RequiredKeys(ComponentType);

        return required.All(key =>
            _configuration.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value));
    }
}
