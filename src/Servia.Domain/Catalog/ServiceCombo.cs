using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Catalog;

/// <summary>
/// Combinação de serviços executados na mesma sessão.
///
/// O combo é um agregado: protege a invariante de que precisa ter ao menos
/// um item, e calcula a duração total somando as durações dos serviços.
///
/// O preço do combo é próprio — normalmente menor que a soma dos serviços
/// individuais, o que é a razão comercial de existir.
/// </summary>
public sealed class ServiceCombo : ITenantScoped
{
    private readonly List<ComboItem> _items = [];

    private ServiceCombo(Guid id, Guid tenantId, string name, Money comboPrice)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        ComboPrice = comboPrice;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>.
    /// </summary>
    private ServiceCombo()
    {
        Name = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; }

    public Money ComboPrice { get; private set; }

    public IReadOnlyCollection<ComboItem> Items => _items;

    public static ServiceCombo Create(Guid tenantId, string name, Money comboPrice)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainRuleException("O nome do combo é obrigatório.");
        }

        return new ServiceCombo(Guid.NewGuid(), tenantId, name.Trim(), comboPrice);
    }

    /// <summary>
    /// Adiciona um serviço ao combo. Se o serviço já existe, incrementa a
    /// quantidade em vez de duplicar o item.
    /// </summary>
    public void AddItem(Guid serviceId, int quantity = 1)
    {
        if (serviceId == Guid.Empty)
        {
            throw new DomainRuleException("O serviço é obrigatório.");
        }

        if (quantity <= 0)
        {
            throw new DomainRuleException("A quantidade deve ser maior que zero.");
        }

        var existing = _items.FirstOrDefault(item => item.ServiceId == serviceId);

        if (existing is not null)
        {
            existing.IncreaseQuantity(quantity);
            return;
        }

        _items.Add(new ComboItem(serviceId, quantity));
    }

    /// <summary>Remove um serviço do combo.</summary>
    public void RemoveItem(Guid serviceId)
    {
        var existing = _items.FirstOrDefault(item => item.ServiceId == serviceId);

        if (existing is null)
        {
            throw new DomainRuleException("O serviço não faz parte do combo.");
        }

        _items.Remove(existing);
    }

    /// <summary>
    /// Calcula a duração total do combo, somando a duração de cada serviço
    /// multiplicada pela quantidade.
    /// </summary>
    public int TotalDuration(IReadOnlyDictionary<Guid, int> serviceDurations)
    {
        ArgumentNullException.ThrowIfNull(serviceDurations);

        var total = 0;

        foreach (var item in _items)
        {
            if (!serviceDurations.TryGetValue(item.ServiceId, out var duration))
            {
                throw new DomainRuleException(
                    $"Duração desconhecida para o serviço {item.ServiceId}.");
            }

            total += duration * item.Quantity;
        }

        return total;
    }

    /// <summary>Renomeia o combo.</summary>
    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainRuleException("O nome do combo é obrigatório.");
        }

        Name = name.Trim();
    }

    /// <summary>Altera o preço do combo.</summary>
    public void ChangePrice(Money comboPrice) => ComboPrice = comboPrice;
}

/// <summary>Item de um combo: serviço e quantidade.</summary>
public sealed class ComboItem
{
    internal ComboItem(Guid serviceId, int quantity)
    {
        ServiceId = serviceId;
        Quantity = quantity;
    }

    public Guid ServiceId { get; private set; }

    public int Quantity { get; private set; }

    internal void IncreaseQuantity(int amount) => Quantity += amount;
}
