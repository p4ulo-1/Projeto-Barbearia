using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Customers;

/// <summary>
/// Cliente de um estabelecimento.
///
/// Separado de <c>User</c> (ADR-009): nem todo cliente final terá login, e o
/// cadastro é específico do tenant (ADR-015). O vínculo com um usuário de acesso
/// é opcional.
///
/// Protege a invariante de contato: nome e telefone são obrigatórios.
/// Oferece <see cref="Anonymize"/> para atender ao direito de exclusão da LGPD
/// sem apagar o histórico de atendimentos.
/// </summary>
public sealed class Customer : ITenantScoped
{
    private const string AnonymizedName = "Cliente anonimizado";
    private const string AnonymizedPhone = "0000000000";

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>,
    /// que é o único caminho público de construção.
    /// </summary>
    private Customer()
    {
        Name = string.Empty;
        Phone = string.Empty;
    }

    private Customer(
        Guid id,
        Guid tenantId,
        Guid? userId,
        string name,
        string phone,
        string? email,
        DateOnly? birthDate,
        bool isActive,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        UserId = userId;
        Name = name;
        Phone = phone;
        Email = email;
        BirthDate = birthDate;
        IsActive = isActive;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>Vínculo opcional com o usuário de login do cliente.</summary>
    public Guid? UserId { get; private set; }

    public string Name { get; private set; }

    public string Phone { get; private set; }

    public string? Email { get; private set; }

    public DateOnly? BirthDate { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Indica se o cadastro já foi anonimizado.</summary>
    public bool IsAnonymized => Name == AnonymizedName;

    public static Customer Create(
        Guid tenantId,
        string name,
        string phone,
        string? email = null,
        DateOnly? birthDate = null,
        Guid? userId = null,
        DateTimeOffset? now = null)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainRuleException("O nome do cliente é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(phone))
        {
            throw new DomainRuleException("O telefone do cliente é obrigatório.");
        }

        return new Customer(
            Guid.NewGuid(),
            tenantId,
            userId,
            name.Trim(),
            phone.Trim(),
            Normalize(email),
            birthDate,
            isActive: true,
            now ?? DateTimeOffset.UtcNow);
    }

    /// <summary>Atualiza os dados de contato do cliente.</summary>
    public void UpdateContact(string name, string phone, string? email, DateOnly? birthDate)
    {
        if (IsAnonymized)
        {
            throw new DomainRuleException("Não é possível alterar um cadastro anonimizado.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainRuleException("O nome do cliente é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(phone))
        {
            throw new DomainRuleException("O telefone do cliente é obrigatório.");
        }

        Name = name.Trim();
        Phone = phone.Trim();
        Email = Normalize(email);
        BirthDate = birthDate;
    }

    /// <summary>Vincula o cliente a um usuário de login.</summary>
    public void LinkUser(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainRuleException("O usuário é obrigatório.");
        }

        UserId = userId;
    }

    /// <summary>
    /// Remove os dados pessoais, preservando o registro para o histórico
    /// de atendimentos. Atende ao direito de exclusão da LGPD.
    /// </summary>
    public void Anonymize()
    {
        Name = AnonymizedName;
        Phone = AnonymizedPhone;
        Email = null;
        BirthDate = null;
        UserId = null;
        IsActive = false;
    }

    /// <summary>Desativa o cadastro sem remover os dados.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Reativa o cadastro.</summary>
    public void Activate() => IsActive = true;

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
