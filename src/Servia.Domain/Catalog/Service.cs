using Servia.Domain.Common;

namespace Servia.Domain.Catalog;

/// <summary>
/// Serviço ofertado por um estabelecimento.
///
/// Protege as invariantes de catálogo: nome obrigatório, duração positiva e
/// preço não negativo. A desativação preserva o histórico — o serviço não é
/// removido, apenas deixa de ser ofertado.
/// </summary>
public sealed class Service
{
    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>,
    /// que é o único caminho público de construção.
    /// </summary>
    private Service()
    {
        Name = string.Empty;
    }

    private Service(
        Guid id,
        Guid tenantId,
        Guid? categoryId,
        string name,
        string? description,
        Money price,
        int durationMinutes,
        bool highlight,
        bool isActive)
    {
        Id = id;
        TenantId = tenantId;
        CategoryId = categoryId;
        Name = name;
        Description = description;
        Price = price;
        DurationMinutes = durationMinutes;
        Highlight = highlight;
        IsActive = isActive;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid? CategoryId { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public Money Price { get; private set; }

    public int DurationMinutes { get; private set; }

    public bool Highlight { get; private set; }

    public bool IsActive { get; private set; }

    public static Service Create(
        Guid tenantId,
        string name,
        Money price,
        int durationMinutes,
        string? description = null,
        Guid? categoryId = null,
        bool highlight = false)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainRuleException("O nome do serviço é obrigatório.");
        }

        if (durationMinutes <= 0)
        {
            throw new DomainRuleException("A duração do serviço deve ser maior que zero.");
        }

        return new Service(
            Guid.NewGuid(),
            tenantId,
            categoryId,
            name.Trim(),
            string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            price,
            durationMinutes,
            highlight,
            isActive: true);
    }

    /// <summary>Atualiza os dados descritivos do serviço.</summary>
    public void Update(string name, string? description, Guid? categoryId, bool highlight)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainRuleException("O nome do serviço é obrigatório.");
        }

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        CategoryId = categoryId;
        Highlight = highlight;
    }

    /// <summary>Altera o preço, preservando a invariante de valor não negativo.</summary>
    public void ChangePrice(Money price)
    {
        Price = price;
    }

    /// <summary>Altera a duração, preservando a invariante de duração positiva.</summary>
    public void ChangeDuration(int durationMinutes)
    {
        if (durationMinutes <= 0)
        {
            throw new DomainRuleException("A duração do serviço deve ser maior que zero.");
        }

        DurationMinutes = durationMinutes;
    }

    /// <summary>Desativa o serviço preservando o histórico de atendimentos.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Reativa o serviço.</summary>
    public void Activate() => IsActive = true;
}
