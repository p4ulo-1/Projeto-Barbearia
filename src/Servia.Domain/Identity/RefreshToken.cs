using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Identity;

/// <summary>
/// Token de renovação de sessão.
///
/// O token em claro nunca é persistido: guarda-se apenas o hash. Assim, um
/// vazamento do banco não permite reutilizar sessões.
///
/// A revogação é explícita e imediata: um token revogado não pode ser usado
/// para renovar, mesmo antes de expirar.
/// </summary>
public sealed class RefreshToken : ITenantScoped
{
    public const int DefaultLifetimeDays = 7;

    private RefreshToken(
        Guid id,
        Guid tenantId,
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Issue"/>.
    /// </summary>
    private RefreshToken()
    {
        TokenHash = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid UserId { get; private set; }

    /// <summary>Hash do token. O valor em claro nunca é persistido.</summary>
    public string TokenHash { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>Motivo da revogação, quando houver.</summary>
    public string? RevokedReason { get; private set; }

    /// <summary>Indica se o token foi revogado.</summary>
    public bool IsRevoked => RevokedAt.HasValue;

    /// <summary>
    /// Emite um novo token de renovação.
    /// </summary>
    public static RefreshToken Issue(
        Guid tenantId,
        Guid userId,
        string tokenHash,
        DateTimeOffset now,
        int lifetimeDays = DefaultLifetimeDays)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainRuleException("O usuário é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new DomainRuleException("O hash do token é obrigatório.");
        }

        if (lifetimeDays <= 0)
        {
            throw new DomainRuleException("A validade do token deve ser maior que zero.");
        }

        return new RefreshToken(
            Guid.NewGuid(),
            tenantId,
            userId,
            tokenHash,
            now.AddDays(lifetimeDays),
            now);
    }

    /// <summary>
    /// Indica se o token pode ser usado para renovar a sessão.
    /// Um token revogado ou expirado não é válido.
    /// </summary>
    public bool IsValid(DateTimeOffset now) => !IsRevoked && now < ExpiresAt;

    /// <summary>Revoga o token, impedindo seu uso imediato.</summary>
    public void Revoke(DateTimeOffset now, string? reason = null)
    {
        if (IsRevoked)
        {
            throw new DomainRuleException("O token já está revogado.");
        }

        RevokedAt = now;
        RevokedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    /// <summary>
    /// Revoga o token se ele ainda estiver válido.
    /// Usado na rotação: o token antigo é revogado ao emitir um novo.
    /// </summary>
    public void RevokeIfActive(DateTimeOffset now, string? reason = null)
    {
        if (!IsRevoked)
        {
            Revoke(now, reason);
        }
    }
}
