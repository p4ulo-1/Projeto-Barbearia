using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Catalog;

/// <summary>
/// Variação de um serviço, com preço e duração próprios.
///
/// Permite representar diferenças reais de execução: corte feminino e
/// masculino, cabelo curto e longo, área pequena e grande. A variação
/// pertence ao serviço e não existe sem ele.
/// </summary>
public sealed class ServiceVariant : ITenantScoped
{
    private ServiceVariant(
        Guid id,
        Guid tenantId,
        Guid serviceId,
        string label,
        Money price,
        int durationMinutes)
    {
        Id = id;
        TenantId = tenantId;
        ServiceId = serviceId;
        Label = label;
        Price = price;
        DurationMinutes = durationMinutes;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>.
    /// </summary>
    private ServiceVariant()
    {
        Label = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ServiceId { get; private set; }

    public string Label { get; private set; }

    public Money Price { get; private set; }

    public int DurationMinutes { get; private set; }

    public static ServiceVariant Create(
        Guid tenantId,
        Guid serviceId,
        string label,
        Money price,
        int durationMinutes)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (serviceId == Guid.Empty)
        {
            throw new DomainRuleException("O serviço é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            throw new DomainRuleException("O nome da variação é obrigatório.");
        }

        if (durationMinutes <= 0)
        {
            throw new DomainRuleException("A duração da variação deve ser maior que zero.");
        }

        return new ServiceVariant(
            Guid.NewGuid(), tenantId, serviceId, label.Trim(), price, durationMinutes);
    }

    /// <summary>Atualiza preço e duração da variação.</summary>
    public void Update(Money price, int durationMinutes)
    {
        if (durationMinutes <= 0)
        {
            throw new DomainRuleException("A duração da variação deve ser maior que zero.");
        }

        Price = price;
        DurationMinutes = durationMinutes;
    }

    /// <summary>Renomeia a variação.</summary>
    public void Rename(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new DomainRuleException("O nome da variação é obrigatório.");
        }

        Label = label.Trim();
    }
}
