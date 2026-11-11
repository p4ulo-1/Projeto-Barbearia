using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Servia.Api.Contracts;
using Servia.Application.Identity;
using Servia.Domain.Tenancy;

namespace Servia.Api.Controllers;

/// <summary>
/// Endpoints de autenticação (F10.1, F10.6, F10.7).
///
/// O controller é fino: valida a forma do pedido, delega ao caso de uso e
/// traduz o resultado em resposta HTTP.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    AuthenticationService authentication,
    ITenantContext tenantContext) : ControllerBase
{
    /// <summary>Autentica e emite os tokens.</summary>
    [HttpPost("sign-in")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SignInAsync(
        [FromBody] SignInRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // No login ainda nao ha token, entao o tenant vem do corpo.
        // O contexto precisa ser definido aqui para que o filtro global
        // permita consultar o usuario. Depois do login, o tenant passa a
        // vir da claim do token (F10.5).
        if (request.TenantId != Guid.Empty)
        {
            tenantContext.SetTenant(request.TenantId);
        }

        var result = await authentication.SignInAsync(
            new SignInCommand(request.TenantId, request.Email, request.Password),
            cancellationToken);

        if (!result.Success)
        {
            return Unauthorized(new ErrorResponse(result.FailureReason!));
        }

        return Ok(new AuthResponse(
            result.AccessToken!.Value,
            result.AccessToken.ExpiresAt,
            result.RefreshToken!));
    }

    /// <summary>
    /// Renova a sessão. O token usado é revogado (rotação).
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshAsync(
        [FromBody] RefreshRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // O tenant e resolvido a partir do refresh token, nao de header.
        var result = await authentication.RefreshAsync(request.RefreshToken, cancellationToken);

        if (!result.Success)
        {
            return Unauthorized(new ErrorResponse(result.FailureReason!));
        }

        return Ok(new AuthResponse(
            result.AccessToken!.Value,
            result.AccessToken.ExpiresAt,
            result.RefreshToken!));
    }

    /// <summary>Encerra a sessão, revogando o token de renovação.</summary>
    [HttpPost("sign-out")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SignOutAsync(
        [FromBody] SignOutRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // O tenant e resolvido a partir do refresh token.
        await authentication.SignOutAsync(request.RefreshToken, cancellationToken);

        // Logout é idempotente: repetir não é erro.
        return NoContent();
    }
}
