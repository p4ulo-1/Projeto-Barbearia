using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Identity;

/// <summary>
/// Usuário de um estabelecimento.
///
/// <b>Não existe identidade global</b> (ADR-015): a mesma pessoa física que
/// frequenta dois estabelecimentos possui dois registros independentes, sem
/// vínculo entre eles. O e-mail é único <b>por tenant</b>, não globalmente.
///
/// A única exceção é <see cref="Role.PlatformAdmin"/>, que não pertence a um
/// tenant e tem <c>TenantId</c> nulo.
/// </summary>
public sealed class User : ITenantScoped
{
    private User(
        Guid id,
        Guid tenantId,
        string name,
        string email,
        string passwordHash,
        Role role,
        bool isActive,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        Email = email;
        PasswordHash = passwordHash;
        Role = role;
        IsActive = isActive;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>.
    /// </summary>
    private User()
    {
        Name = string.Empty;
        Email = string.Empty;
        PasswordHash = string.Empty;
    }

    public Guid Id { get; private set; }

    /// <summary>
    /// Estabelecimento ao qual o usuário pertence.
    /// Vazio apenas para <see cref="Role.PlatformAdmin"/>.
    /// </summary>
    public Guid TenantId { get; private set; }

    public string Name { get; private set; }

    /// <summary>E-mail único dentro do tenant.</summary>
    public string Email { get; private set; }

    public string PasswordHash { get; private set; }

    public Role Role { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }

    /// <summary>Indica se o usuário pertence à plataforma, não a um tenant.</summary>
    public bool IsPlatformAdmin => Role == Role.PlatformAdmin;

    /// <summary>Indica se o usuário pode autenticar.</summary>
    public bool CanSignIn => IsActive;

    /// <summary>
    /// Cria um usuário de estabelecimento.
    /// </summary>
    public static User CreateForTenant(
        Guid tenantId,
        string name,
        string email,
        string passwordHash,
        Role role,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (RolePermissions.IsPlatformRole(role))
        {
            throw new DomainRuleException(
                "Um administrador da plataforma não pertence a um estabelecimento.");
        }

        return Create(tenantId, name, email, passwordHash, role, now);
    }

    /// <summary>
    /// Cria um administrador da plataforma, sem vínculo com tenant.
    /// </summary>
    public static User CreatePlatformAdmin(
        string name,
        string email,
        string passwordHash,
        DateTimeOffset now) =>
        Create(Guid.Empty, name, email, passwordHash, Role.PlatformAdmin, now);

    private static User Create(
        Guid tenantId,
        string name,
        string email,
        string passwordHash,
        Role role,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainRuleException("O nome do usuário é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
        {
            throw new DomainRuleException("O e-mail do usuário é inválido.");
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainRuleException("A senha é obrigatória.");
        }

        return new User(
            Guid.NewGuid(),
            tenantId,
            name.Trim(),
            email.Trim().ToLowerInvariant(),
            passwordHash,
            role,
            isActive: true,
            now);
    }

    /// <summary>Indica se o usuário possui a permissão informada.</summary>
    public bool HasPermission(Permission permission) =>
        IsActive && RolePermissions.Has(Role, permission);

    /// <summary>Altera o papel do usuário dentro do tenant.</summary>
    public void ChangeRole(Role role)
    {
        if (IsPlatformAdmin != RolePermissions.IsPlatformRole(role))
        {
            throw new DomainRuleException(
                "Não é possível converter um usuário de estabelecimento em administrador da plataforma, ou vice-versa.");
        }

        Role = role;
    }

    /// <summary>Atualiza os dados de perfil.</summary>
    public void UpdateProfile(string name, string email)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainRuleException("O nome do usuário é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
        {
            throw new DomainRuleException("O e-mail do usuário é inválido.");
        }

        Name = name.Trim();
        Email = email.Trim().ToLowerInvariant();
    }

    /// <summary>Substitui o hash da senha.</summary>
    public void ChangePassword(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainRuleException("A senha é obrigatória.");
        }

        PasswordHash = passwordHash;
    }

    /// <summary>Registra o instante do último login bem-sucedido.</summary>
    public void RegisterLogin(DateTimeOffset now)
    {
        if (!CanSignIn)
        {
            throw new DomainRuleException("Um usuário inativo não pode autenticar.");
        }

        LastLoginAt = now;
    }

    /// <summary>Desativa o usuário, impedindo autenticação.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Reativa o usuário.</summary>
    public void Activate() => IsActive = true;
}
