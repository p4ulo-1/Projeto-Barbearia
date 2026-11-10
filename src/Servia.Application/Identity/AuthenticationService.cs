using System.Security.Cryptography;
using System.Text;
using Servia.Domain.Identity;

namespace Servia.Application.Identity;

/// <summary>
/// Caso de uso de autenticação (F10.1, F10.6, F10.7).
///
/// Concentra login, renovação e logout. A senha nunca é comparada em claro:
/// o hash é verificado pelo <see cref="IPasswordHasher"/>.
///
/// <b>Resistência a enumeração de usuários:</b> e-mail inexistente e senha
/// errada produzem a mesma resposta. Um atacante não descobre quais e-mails
/// existem testando o login.
///
/// <b>Rotação de token (F10.6):</b> renovar revoga o token anterior. Um token
/// roubado só funciona uma vez.
/// </summary>
public sealed class AuthenticationService(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    ITokenIssuer tokenIssuer,
    TimeProvider timeProvider)
{
    /// <summary>Validade do token de renovação, em dias.</summary>
    public const int RefreshTokenLifetimeDays = RefreshToken.DefaultLifetimeDays;

    /// <summary>
    /// Autentica o usuário e emite os tokens de acesso e renovação.
    /// </summary>
    public async Task<AuthenticationResult> SignInAsync(
        SignInCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.TenantId == Guid.Empty)
        {
            return AuthenticationResult.Invalid("Estabelecimento não identificado.");
        }

        if (string.IsNullOrWhiteSpace(command.Email) || string.IsNullOrWhiteSpace(command.Password))
        {
            return AuthenticationResult.Invalid("Informe e-mail e senha.");
        }

        var user = await users.FindByEmailAsync(
            command.TenantId, command.Email.Trim().ToLowerInvariant(), cancellationToken);

        // Mesma resposta para usuário inexistente e senha errada.
        if (user is null)
        {
            return AuthenticationResult.Invalid("E-mail ou senha inválidos.");
        }

        if (!passwordHasher.Verify(command.Password, user.PasswordHash))
        {
            return AuthenticationResult.Invalid("E-mail ou senha inválidos.");
        }

        if (!user.CanSignIn)
        {
            return AuthenticationResult.Invalid("Usuário inativo.");
        }

        var now = timeProvider.GetUtcNow();
        user.RegisterLogin(now);

        var accessToken = tokenIssuer.Issue(user, now);
        var (refreshEntity, clearRefreshToken) = IssueRefreshToken(user, now);

        await refreshTokens.AddAsync(refreshEntity, cancellationToken);
        await users.SaveChangesAsync(cancellationToken);

        return AuthenticationResult.Succeeded(accessToken, clearRefreshToken);
    }

    /// <summary>
    /// Renova a sessão a partir de um token de renovação válido (F10.6).
    /// O token usado é revogado e um novo é emitido.
    /// </summary>
    public async Task<AuthenticationResult> RefreshAsync(
        Guid tenantId,
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(refreshToken))
        {
            return AuthenticationResult.Invalid("Sessão inválida.");
        }

        var hash = HashToken(refreshToken);
        var stored = await refreshTokens.FindByHashAsync(tenantId, hash, cancellationToken);

        if (stored is null)
        {
            return AuthenticationResult.Invalid("Sessão inválida.");
        }

        var now = timeProvider.GetUtcNow();

        if (!stored.IsValid(now))
        {
            return AuthenticationResult.Invalid("Sessão expirada. Entre novamente.");
        }

        var user = await users.FindByIdAsync(tenantId, stored.UserId, cancellationToken);

        if (user is null || !user.CanSignIn)
        {
            return AuthenticationResult.Invalid("Sessão inválida.");
        }

        // Rotação: o token usado deixa de valer imediatamente.
        stored.Revoke(now, "Rotação de token");

        var accessToken = tokenIssuer.Issue(user, now);
        var (newEntity, clearRefreshToken) = IssueRefreshToken(user, now);

        await refreshTokens.AddAsync(newEntity, cancellationToken);
        await refreshTokens.SaveChangesAsync(cancellationToken);

        return AuthenticationResult.Succeeded(accessToken, clearRefreshToken);
    }

    /// <summary>
    /// Encerra a sessão, revogando o token de renovação (F10.7).
    /// </summary>
    public async Task<bool> SignOutAsync(
        Guid tenantId,
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(refreshToken))
        {
            return false;
        }

        var hash = HashToken(refreshToken);
        var stored = await refreshTokens.FindByHashAsync(tenantId, hash, cancellationToken);

        if (stored is null)
        {
            return false;
        }

        stored.RevokeIfActive(timeProvider.GetUtcNow(), "Logout");
        await refreshTokens.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <summary>
    /// Emite um token de renovação.
    /// Devolve a entidade (com o hash) e o valor em claro, que so existe
    /// aqui e e entregue ao cliente uma unica vez.
    /// </summary>
    private (RefreshToken Entity, string ClearToken) IssueRefreshToken(User user, DateTimeOffset now)
    {
        var clearToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        var entity = RefreshToken.Issue(
            user.TenantId,
            user.Id,
            HashToken(clearToken),
            now,
            RefreshTokenLifetimeDays);

        return (entity, clearToken);
    }

    /// <summary>Calcula o hash do token, usado como chave de busca.</summary>
    internal static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

/// <summary>Dados de entrada do login.</summary>
public sealed record SignInCommand(Guid TenantId, string Email, string Password);

/// <summary>Resultado da autenticação.</summary>
public sealed record AuthenticationResult(
    bool Success,
    AccessToken? AccessToken,
    string? RefreshToken,
    string? FailureReason)
{
    public static AuthenticationResult Succeeded(AccessToken accessToken, string refreshToken) =>
        new(true, accessToken, refreshToken, null);

    public static AuthenticationResult Invalid(string reason) =>
        new(false, null, null, reason);
}
