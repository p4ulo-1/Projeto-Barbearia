using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Servia.Domain.Identity;
using Servia.Domain.Identity;

namespace Servia.Infrastructure.Identity;

/// <summary>
/// Emissor de token JWT (F10.1).
///
/// O token carrega <c>sub</c> (UserId), <c>tenant_id</c> e <c>role</c>. É a
/// fonte confiável para o servidor resolver o tenant: o cliente não informa
/// o tenant, ele vem assinado (F10.5).
///
/// A chave de assinatura vem da configuração, nunca do código. Sem chave
/// configurada, a aplicação falha ao iniciar — melhor falhar do que emitir
/// tokens com chave padrão.
/// </summary>
public sealed class JwtTokenIssuer : ITokenIssuer
{
    /// <summary>Claim que carrega o tenant.</summary>
    public const string TenantIdClaim = "tenant_id";

    /// <summary>Claim que carrega o papel do usuário.</summary>
    public const string RoleClaim = "role";

    /// <summary>
    /// Claim que carrega as permissões efetivas do usuário (uma por claim).
    /// Permite que papéis personalizados funcionem sem um segundo mecanismo
    /// de autorização: o handler lê esta claim quando presente.
    /// </summary>
    public const string PermissionClaim = "perm";

    /// <summary>Validade do token de acesso, em minutos.</summary>
    public const int AccessTokenLifetimeMinutes = 15;

    private readonly JwtSecurityTokenHandler _handler = new();
    private readonly SigningCredentials _credentials;
    private readonly string _issuer;
    private readonly string _audience;

    public JwtTokenIssuer(JwtOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.SigningKey) || options.SigningKey.Length < 32)
        {
            throw new InvalidOperationException(
                "A chave de assinatura JWT é obrigatória e deve ter ao menos 32 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(options.Issuer) || string.IsNullOrWhiteSpace(options.Audience))
        {
            throw new InvalidOperationException(
                "Issuer e Audience são obrigatórios na configuração JWT.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));

        _credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        _issuer = options.Issuer;
        _audience = options.Audience;
    }

    public AccessToken Issue(User user, DateTimeOffset now) =>
        Issue(user, RolePermissions.For(user.Role), now);

    /// <summary>
    /// Emite o token com as permissões efetivas informadas. Usado quando o
    /// usuário tem papel personalizado, cujas permissões diferem do papel
    /// predefinido.
    /// </summary>
    public AccessToken Issue(
        User user,
        IReadOnlySet<Permission> effectivePermissions,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(effectivePermissions);

        var expiresAt = now.AddMinutes(AccessTokenLifetimeMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(TenantIdClaim, user.TenantId.ToString()),
            new(RoleClaim, user.Role.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        claims.AddRange(effectivePermissions.Select(
            permission => new Claim(PermissionClaim, permission.ToString())));

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: _credentials);

        return new AccessToken(_handler.WriteToken(token), expiresAt);
    }
}

/// <summary>Configuração do JWT.</summary>
public sealed class JwtOptions
{
    /// <summary>Chave de assinatura. Deve vir de variável de ambiente ou cofre.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = "servia";

    public string Audience { get; set; } = "servia-app";
}
