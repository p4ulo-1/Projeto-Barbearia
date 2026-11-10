using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Identity;

/// <summary>
/// Convite para um membro entrar na equipe de um estabelecimento.
///
/// O convite é de uso único: ao ser aceito, não pode ser aceito novamente.
/// O token em claro nunca é persistido — guarda-se apenas o hash.
///
/// Um convite para <see cref="Role.PlatformAdmin"/> é rejeitado: papéis da
/// plataforma não são concedidos por convite de tenant (F1.13).
/// </summary>
public sealed class Invitation : ITenantScoped
{
    public const int DefaultLifetimeDays = 7;

    private Invitation(
        Guid id,
        Guid tenantId,
        string email,
        Role role,
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        Email = email;
        Role = role;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Issue"/>.
    /// </summary>
    private Invitation()
    {
        Email = string.Empty;
        TokenHash = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string Email { get; private set; }

    public Role Role { get; private set; }

    /// <summary>Hash do token do convite. O valor em claro nunca é persistido.</summary>
    public string TokenHash { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    /// <summary>Usuário criado ao aceitar o convite.</summary>
    public Guid? AcceptedByUserId { get; private set; }

    /// <summary>Indica se o convite já foi aceito.</summary>
    public bool IsAccepted => AcceptedAt.HasValue;

    /// <summary>
    /// Emite um convite para um membro da equipe.
    /// </summary>
    public static Invitation Issue(
        Guid tenantId,
        string email,
        Role role,
        string tokenHash,
        DateTimeOffset now,
        int lifetimeDays = DefaultLifetimeDays)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
        {
            throw new DomainRuleException("O e-mail do convite é inválido.");
        }

        if (RolePermissions.IsPlatformRole(role))
        {
            throw new DomainRuleException(
                "Papéis da plataforma não são concedidos por convite de estabelecimento.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainRuleException("O hash do convite é obrigatório.");
        }

        if (lifetimeDays <= 0)
        {
            throw new DomainRuleException("A validade do convite deve ser maior que zero.");
        }

        return new Invitation(
            Guid.NewGuid(),
            tenantId,
            email.Trim().ToLowerInvariant(),
            role,
            tokenHash,
            now.AddDays(lifetimeDays),
            now);
    }

    /// <summary>
    /// Indica se o convite pode ser aceito na data informada.
    /// Convite aceito ou expirado não é válido.
    /// </summary>
    public bool IsValid(DateTimeOffset now) => !IsAccepted && now < ExpiresAt;

    /// <summary>
    /// Aceita o convite, vinculando-o ao usuário criado.
    /// </summary>
    public void Accept(Guid userId, DateTimeOffset now)
    {
        if (IsAccepted)
        {
            throw new DomainRuleException("O convite já foi aceito.");
        }

        if (now >= ExpiresAt)
        {
            throw new DomainRuleException("O convite expirou.");
        }

        if (userId == Guid.Empty)
        {
            throw new DomainRuleException("O usuário é obrigatório.");
        }

        AcceptedAt = now;
        AcceptedByUserId = userId;
    }
}
