using Servia.Domain.Common;

namespace Servia.Domain.Tenancy;

/// <summary>
/// Plano comercial da plataforma.
///
/// Todos os planos oferecem todos os módulos. A diferenciação está na
/// quantidade de profissionais incluídos, no grau de personalização do site
/// e na franquia mensal de IA (ARCH.md §13.1).
///
/// Não existe preço por profissional excedente: ao ultrapassar o limite, o
/// tenant precisa subir de plano.
/// </summary>
public sealed class Plan
{
    private Plan(
        Guid id,
        string code,
        string name,
        Money monthlyPrice,
        int includedProfessionals,
        SiteCustomizationLevel customizationLevel,
        int includedAiUnits,
        bool isActive)
    {
        Id = id;
        Code = code;
        Name = name;
        MonthlyPrice = monthlyPrice;
        IncludedProfessionals = includedProfessionals;
        CustomizationLevel = customizationLevel;
        IncludedAiUnits = includedAiUnits;
        IsActive = isActive;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>.
    /// </summary>
    private Plan()
    {
        Code = string.Empty;
        Name = string.Empty;
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; }

    public string Name { get; private set; }

    public Money MonthlyPrice { get; private set; }

    /// <summary>Quantidade de profissionais incluídos. Zero significa ilimitado.</summary>
    public int IncludedProfessionals { get; private set; }

    public SiteCustomizationLevel CustomizationLevel { get; private set; }

    /// <summary>Franquia mensal de IA, em unidades de consumo.</summary>
    public int IncludedAiUnits { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>Indica se o plano não limita a quantidade de profissionais.</summary>
    public bool HasUnlimitedProfessionals => IncludedProfessionals == 0;

    public static Plan Create(
        string code,
        string name,
        Money monthlyPrice,
        int includedProfessionals,
        SiteCustomizationLevel customizationLevel,
        int includedAiUnits)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainRuleException("O código do plano é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainRuleException("O nome do plano é obrigatório.");
        }

        if (includedProfessionals < 0)
        {
            throw new DomainRuleException("A quantidade de profissionais não pode ser negativa.");
        }

        if (includedAiUnits < 0)
        {
            throw new DomainRuleException("A franquia de IA não pode ser negativa.");
        }

        return new Plan(
            Guid.NewGuid(),
            code.Trim().ToUpperInvariant(),
            name.Trim(),
            monthlyPrice,
            includedProfessionals,
            customizationLevel,
            includedAiUnits,
            isActive: true);
    }

    /// <summary>
    /// Indica se o plano comporta a quantidade de profissionais informada.
    /// Plano ilimitado comporta qualquer quantidade.
    /// </summary>
    public bool AllowsProfessionals(int count)
    {
        if (count < 0)
        {
            throw new DomainRuleException("A quantidade de profissionais não pode ser negativa.");
        }

        return HasUnlimitedProfessionals || count <= IncludedProfessionals;
    }

    /// <summary>Indica se o plano permite o grau de personalização solicitado.</summary>
    public bool AllowsCustomization(SiteCustomizationLevel level) =>
        level <= CustomizationLevel;

    /// <summary>Altera o preço mensal.</summary>
    public void ChangePrice(Money monthlyPrice) => MonthlyPrice = monthlyPrice;

    /// <summary>Desativa o plano, impedindo novas assinaturas.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Reativa o plano.</summary>
    public void Activate() => IsActive = true;
}

/// <summary>
/// Grau de personalização do site permitido pelo plano.
/// Os valores são ordenados: um plano permite o nível igual ou inferior.
/// </summary>
public enum SiteCustomizationLevel
{
    Basic = 1,
    Intermediate = 2,
    Advanced = 3,
    Full = 4
}
