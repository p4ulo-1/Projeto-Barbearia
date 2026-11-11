using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Servia.Api.Contracts;
using Servia.Domain.Common;
using Servia.Domain.Identity;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Provisioning;

namespace Servia.Api.Controllers;

/// <summary>
/// Cadastro público de estabelecimento (F21.1).
///
/// É o destino do CTA "Começar agora" da landing page. Um visitante cria
/// o próprio tenant com trial de 14 dias, sem intervenção da plataforma.
///
/// <b>Reutiliza o provisionamento:</b> a criação de tenant, assinatura e
/// proprietário já existe em `TenantProvisioner`. Duplicar a lógica aqui
/// criaria dois caminhos que divergiriam com o tempo.
///
/// <b>Diferença em relação ao endpoint administrativo:</b> o plano é
/// sempre o Essencial — o visitante não escolhe plano antes de conhecer
/// o produto. A troca acontece depois, em `/billing/subscription`.
/// </summary>
[ApiController]
[Route("api/v1/public")]
[AllowAnonymous]
public sealed class PublicSignupController(
    TenantProvisioner provisioner,
    IPasswordHasher passwordHasher,
    ISignupThrottle throttle) : ControllerBase
{
    /// <summary>Plano inicial do cadastro público.</summary>
    public const string InitialPlanCode = "ESSENCIAL";

    /// <summary>
    /// Cria um estabelecimento com trial de 14 dias (F21.1).
    /// </summary>
    [HttpPost("signup")]
    [ProducesResponseType<SignupResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> SignupAsync(
        [FromBody] SignupRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Protecao contra abuso (F21.3): limite por IP.
        var clientKey = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "desconhecido";

        if (!throttle.TryAcquire(clientKey))
        {
            return StatusCode(
                StatusCodes.Status429TooManyRequests,
                new ErrorResponse("Muitas tentativas. Aguarde alguns minutos e tente novamente."));
        }

        try
        {
            var result = await provisioner.ProvisionAsync(
                new ProvisionTenantRequest(
                    request.Slug,
                    request.Name,
                    request.Segment,
                    request.Document,
                    InitialPlanCode,
                    request.OwnerName,
                    request.OwnerEmail,
                    passwordHasher.Hash(request.OwnerPassword)),
                cancellationToken);

            return Created(
                "/api/v1/billing/subscription",
                new SignupResponse(
                    result.Tenant.Id,
                    result.Tenant.Slug,
                    result.Tenant.Name,
                    result.Subscription.Status.ToString(),
                    result.Subscription.TrialEndsAt,
                    result.Owner.Email));
        }
        catch (DomainRuleException exception)
        {
            // Slug ou e-mail ja usado: 409, nao 400. O cliente precisa
            // saber que o problema e conflito, nao dado invalido.
            var isConflict = exception.Message.Contains("já está em uso", StringComparison.Ordinal);

            return isConflict
                ? Conflict(new ErrorResponse(exception.Message))
                : BadRequest(new ErrorResponse(exception.Message));
        }
    }
}

/// <summary>Corpo do cadastro público.</summary>
public sealed record SignupRequest(
    [Required, RegularExpression("^[a-z0-9][a-z0-9-]{1,38}[a-z0-9]$",
        ErrorMessage = "O endereço deve ter 3 a 40 caracteres: letras minúsculas, números e hífen.")]
    string Slug,
    [Required, MaxLength(150)] string Name,
    SegmentType Segment,
    [Required, MaxLength(20)] string Document,
    [Required, MaxLength(150)] string OwnerName,
    [Required, EmailAddress, MaxLength(200)] string OwnerEmail,
    [Required, MinLength(8), MaxLength(200)] string OwnerPassword);

/// <summary>Resultado do cadastro público.</summary>
public sealed record SignupResponse(
    Guid TenantId,
    string Slug,
    string Name,
    string SubscriptionStatus,
    DateOnly? TrialEndsAt,
    string OwnerEmail);
