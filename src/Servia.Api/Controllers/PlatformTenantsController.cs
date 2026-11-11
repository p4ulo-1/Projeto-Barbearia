using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Servia.Api.Contracts;
using Servia.Domain.Identity;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Identity;
using Servia.Infrastructure.Provisioning;

namespace Servia.Api.Controllers;

/// <summary>
/// Endpoints de provisionamento de estabelecimentos (F15.1).
///
/// Cria o tenant, a assinatura em trial e o usuário proprietário em uma
/// única operação atômica. É o que coloca o primeiro cliente real no ar.
///
/// <b>Acesso restrito:</b> apenas administradores da plataforma. Um
/// estabelecimento não pode criar outro.
/// </summary>
[ApiController]
[Route("api/v1/platform/tenants")]
[Authorize]
public sealed class PlatformTenantsController(
    TenantProvisioner provisioner,
    IPasswordHasher passwordHasher) : ControllerBase
{
    /// <summary>
    /// Provisiona um novo estabelecimento.
    /// </summary>
    [HttpPost]
    [RequirePlatformAdmin]
    [ProducesResponseType<ProvisionedTenantResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ProvisionAsync(
        [FromBody] ProvisionTenantApiRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await provisioner.ProvisionAsync(
            new ProvisionTenantRequest(
                request.Slug,
                request.Name,
                request.Segment,
                request.Document,
                request.PlanCode,
                request.OwnerName,
                request.OwnerEmail,
                passwordHasher.Hash(request.OwnerPassword)),
            cancellationToken);

        return Created(
            $"/api/v1/platform/tenants/{result.Tenant.Id}",
            new ProvisionedTenantResponse(
                result.Tenant.Id,
                result.Tenant.Slug,
                result.Tenant.Name,
                result.Tenant.Status.ToString(),
                result.Subscription.Status.ToString(),
                result.Subscription.TrialEndsAt,
                result.Owner.Email));
    }
}

/// <summary>
/// Exige que o usuário seja administrador da plataforma (F1.13).
///
/// Um administrador de estabelecimento não pode provisionar outro: os
/// privilégios são disjuntos.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequirePlatformAdminAttribute : AuthorizeAttribute
{
    public RequirePlatformAdminAttribute()
    {
        Policy = PolicyName;
    }

    public const string PolicyName = "platform-admin";
}

/// <summary>Corpo de provisionamento de estabelecimento.</summary>
public sealed record ProvisionTenantApiRequest(
    [Required, RegularExpression("^[a-z0-9][a-z0-9-]{1,38}[a-z0-9]$",
        ErrorMessage = "O identificador deve ter 3 a 40 caracteres: letras minúsculas, números e hífen.")]
    string Slug,
    [Required, MaxLength(150)] string Name,
    SegmentType Segment,
    [Required, MaxLength(20)] string Document,
    [Required, MaxLength(30)] string PlanCode,
    [Required, MaxLength(150)] string OwnerName,
    [Required, EmailAddress, MaxLength(200)] string OwnerEmail,
    [Required, MinLength(8), MaxLength(200)] string OwnerPassword);

/// <summary>Resultado do provisionamento.</summary>
public sealed record ProvisionedTenantResponse(
    Guid TenantId,
    string Slug,
    string Name,
    string TenantStatus,
    string SubscriptionStatus,
    DateOnly? TrialEndsAt,
    string OwnerEmail);
