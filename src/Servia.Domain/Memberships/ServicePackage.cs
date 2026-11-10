using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Memberships;

/// <summary>
/// Pacote pré-pago de sessões (F7.2).
///
/// O cliente compra um bloco de sessões com validade. Diferente do clube,
/// não há recorrência: é uma compra única, consumida até acabar ou expirar.
/// </summary>
public sealed class ServicePackage : ITenantScoped
{
    private ServicePackage(
        Guid id,
        Guid tenantId,
        string name,
        Money price,
        int sessionsIncluded,
        int validityDays)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        Price = price;
        SessionsIncluded = sessionsIncluded;
        ValidityDays = validityDays;
        IsActive = true;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>.
    /// </summary>
    private ServicePackage()
    {
        Name = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; }

    public Money Price { get; private set; }

    public int SessionsIncluded { get; private set; }

    /// <summary>Validade em dias, contados da compra.</summary>
    public int ValidityDays { get; private set; }

    public bool IsActive { get; private set; }

    public static ServicePackage Create(
        Guid tenantId,
        string name,
        Money price,
        int sessionsIncluded,
        int validityDays)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainRuleException("O nome do pacote é obrigatório.");
        }

        if (price.Amount <= 0)
        {
            throw new DomainRuleException("O preço do pacote deve ser maior que zero.");
        }

        if (sessionsIncluded <= 0)
        {
            throw new DomainRuleException("O pacote deve incluir ao menos uma sessão.");
        }

        if (validityDays <= 0)
        {
            throw new DomainRuleException("A validade deve ser maior que zero.");
        }

        return new ServicePackage(
            Guid.NewGuid(), tenantId, name.Trim(), price, sessionsIncluded, validityDays);
    }

    /// <summary>Desativa o pacote, impedindo novas vendas.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Reativa o pacote.</summary>
    public void Activate() => IsActive = true;
}

/// <summary>
/// Saldo de um pacote adquirido por um cliente (F7.2, F7.3).
///
/// Controla as sessões restantes e a validade. Uma sessão não pode ser
/// consumida após a expiração.
/// </summary>
public sealed class PackageUsage : ITenantScoped
{
    private PackageUsage(
        Guid id,
        Guid tenantId,
        Guid packageId,
        Guid customerId,
        int sessionsRemaining,
        DateOnly expiresAt)
    {
        Id = id;
        TenantId = tenantId;
        PackageId = packageId;
        CustomerId = customerId;
        SessionsRemaining = sessionsRemaining;
        ExpiresAt = expiresAt;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Purchase"/>.
    /// </summary>
    private PackageUsage()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid PackageId { get; private set; }

    public Guid CustomerId { get; private set; }

    public int SessionsRemaining { get; private set; }

    public DateOnly ExpiresAt { get; private set; }

    /// <summary>Indica se o saldo acabou.</summary>
    public bool IsExhausted => SessionsRemaining <= 0;

    /// <summary>Indica se o pacote expirou na data informada.</summary>
    public bool IsExpired(DateOnly today) => today > ExpiresAt;

    /// <summary>
    /// Registra a compra de um pacote por um cliente.
    /// </summary>
    public static PackageUsage Purchase(
        Guid tenantId,
        Guid customerId,
        ServicePackage package,
        DateOnly purchaseDate)
    {
        ArgumentNullException.ThrowIfNull(package);

        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (customerId == Guid.Empty)
        {
            throw new DomainRuleException("O cliente é obrigatório.");
        }

        if (!package.IsActive)
        {
            throw new DomainRuleException("O pacote não está disponível para venda.");
        }

        return new PackageUsage(
            Guid.NewGuid(),
            tenantId,
            package.Id,
            customerId,
            package.SessionsIncluded,
            purchaseDate.AddDays(package.ValidityDays));
    }

    /// <summary>
    /// Consome uma sessão do pacote (F7.3).
    /// Falha se o saldo acabou ou o pacote expirou.
    /// </summary>
    public void Consume(DateOnly today)
    {
        if (IsExpired(today))
        {
            throw new DomainRuleException("O pacote expirou.");
        }

        if (IsExhausted)
        {
            throw new DomainRuleException("O pacote não tem sessões restantes.");
        }

        SessionsRemaining--;
    }
}
