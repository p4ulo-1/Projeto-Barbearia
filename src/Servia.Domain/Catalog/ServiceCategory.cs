using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Catalog;

/// <summary>
/// Categoria de serviços, usada para agrupar o catálogo no site e no
/// agendamento.
///
/// A ordem de exibição é explícita: o estabelecimento decide como apresentar
/// o catálogo, e a ordenação não depende do nome.
/// </summary>
public sealed class ServiceCategory : ITenantScoped
{
    private ServiceCategory(Guid id, Guid tenantId, string name, int sortOrder)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        SortOrder = sortOrder;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>.
    /// </summary>
    private ServiceCategory()
    {
        Name = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; }

    public int SortOrder { get; private set; }

    public static ServiceCategory Create(Guid tenantId, string name, int sortOrder = 0)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainRuleException("O nome da categoria é obrigatório.");
        }

        if (sortOrder < 0)
        {
            throw new DomainRuleException("A ordem de exibição não pode ser negativa.");
        }

        return new ServiceCategory(Guid.NewGuid(), tenantId, name.Trim(), sortOrder);
    }

    /// <summary>Renomeia a categoria.</summary>
    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainRuleException("O nome da categoria é obrigatório.");
        }

        Name = name.Trim();
    }

    /// <summary>Altera a ordem de exibição.</summary>
    public void ChangeOrder(int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new DomainRuleException("A ordem de exibição não pode ser negativa.");
        }

        SortOrder = sortOrder;
    }
}
