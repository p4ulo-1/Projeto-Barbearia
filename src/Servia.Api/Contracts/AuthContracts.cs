using System.ComponentModel.DataAnnotations;

namespace Servia.Api.Contracts;

/// <summary>
/// Corpo do login.
///
/// O tenant é informado aqui porque, no login, ainda não há token. Depois de
/// autenticado, o tenant vem do token e não é mais aceito do cliente (F10.5).
/// </summary>
public sealed record SignInRequest(
    [Required] Guid TenantId,
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password);

/// <summary>Corpo da renovação de sessão.</summary>
public sealed record RefreshRequest([Required] string RefreshToken);

/// <summary>Resposta de autenticação bem-sucedida.</summary>
public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string RefreshToken);

/// <summary>Corpo do logout.</summary>
public sealed record SignOutRequest([Required] string RefreshToken);
