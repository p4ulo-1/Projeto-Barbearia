using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Servia.Api.Authorization;
using Servia.Api.Contracts;
using Servia.Domain.Common;
using Servia.Domain.Identity;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;

namespace Servia.Api.Controllers;

/// <summary>
/// Papéis personalizados do estabelecimento (seção 3).
///
/// O Owner cria papéis com nome livre e permissões escolhidas, atribui aos
/// funcionários e altera/remove depois. Os papéis pertencem ao tenant e não
/// podem conceder permissões exclusivas do Owner ou da plataforma.
/// </summary>
[ApiController]
[Route("api/v1/team/roles")]
[Authorize]
public sealed class CustomRolesController(
    AppDbContext context,
    ITenantContext tenantContext,
    IRefreshTokenRepository refreshTokens,
    TimeProvider timeProvider) : ControllerBase
{
    /// <summary>
    /// Permissões efetivas do usuário autenticado, a partir da claim
    /// <c>perm</c> assinada no token.
    /// </summary>
    private IReadOnlySet<Permission> ActorPermissions() =>
        User.FindAll(Infrastructure.Identity.JwtTokenIssuer.PermissionClaim)
            .Select(claim => claim.Value)
            .Select(value => Enum.TryParse<Permission>(value, out var permission)
                ? permission
                : (Permission?)null)
            .Where(permission => permission.HasValue)
            .Select(permission => permission!.Value)
            .ToHashSet();

    /// <summary>Identificador do usuário autenticado.</summary>
    private Guid ActorUserId()
    {
        var claim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(claim, out var userId)
            ? userId
            : throw new DomainRuleException("Usuário não identificado.");
    }

    /// <summary>
    /// Revoga os tokens de renovação dos usuários afetados por uma mudança de
    /// permissões. Sem isso, um token de acesso já emitido continuaria
    /// concedendo as permissões antigas até expirar (15 min).
    /// </summary>
    private async Task RevokeTokensAsync(
        IEnumerable<Guid> userIds,
        string reason,
        CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();
        var now = timeProvider.GetUtcNow();

        foreach (var userId in userIds.Distinct())
        {
            await refreshTokens.RevokeAllForUserAsync(
                tenantId, userId, now, reason, cancellationToken);
        }
    }
    /// <summary>Lista os papéis personalizados do estabelecimento.</summary>
    [HttpGet]
    [RequirePermission(Permission.ManageTeam)]
    [ProducesResponseType<IReadOnlyList<CustomRoleResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync(CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();

        var roles = await context.CustomRoles
            .Where(role => role.TenantId == tenantId)
            .OrderBy(role => role.Name)
            .ToListAsync(cancellationToken);

        return Ok(roles.Select(CustomRoleResponse.From).ToList());
    }

    /// <summary>Lista as permissões que podem ser concedidas a um papel.</summary>
    [HttpGet("assignable-permissions")]
    [RequirePermission(Permission.ManageTeam)]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    public IActionResult ListAssignablePermissions() =>
        Ok(RolePermissions.AssignableToCustomRole
            .OrderBy(permission => permission.ToString())
            .Select(permission => permission.ToString())
            .ToList());

    /// <summary>Cria um papel personalizado.</summary>
    [HttpPost]
    [RequirePermission(Permission.ManageTeam)]
    [ProducesResponseType<CustomRoleResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateCustomRoleRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tenantId = RequireTenant();

        var nameTaken = await context.CustomRoles
            .AnyAsync(role => role.TenantId == tenantId && role.Name == request.Name.Trim(),
                cancellationToken);

        if (nameTaken)
        {
            return Conflict(new ErrorResponse($"Já existe um papel com o nome '{request.Name}'."));
        }

        var permissions = ParsePermissions(request.Permissions).ToList();

        // Impede escalonamento: o ator só concede o que ele próprio possui.
        RolePermissions.EnsureCanGrant(ActorPermissions(), permissions);

        var role = CustomRole.Create(tenantId, request.Name, permissions);

        context.CustomRoles.Add(role);
        await context.SaveChangesAsync(cancellationToken);

        return Created($"/api/v1/team/roles/{role.Id}", CustomRoleResponse.From(role));
    }

    /// <summary>Altera nome e permissões de um papel personalizado.</summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(Permission.ManageTeam)]
    [ProducesResponseType<CustomRoleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAsync(
        Guid id,
        [FromBody] UpdateCustomRoleRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var role = await LoadAsync(id, cancellationToken);

        var permissions = ParsePermissions(request.Permissions).ToList();

        // Impede escalonamento: o ator só concede o que ele próprio possui.
        RolePermissions.EnsureCanGrant(ActorPermissions(), permissions);

        role.Rename(request.Name);
        role.ReplacePermissions(permissions);

        // Permissões mudaram: força a reemissão dos tokens dos usuários do papel.
        var affected = await context.Users
            .Where(user => user.CustomRoleId == id)
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

        await RevokeTokensAsync(affected, "Permissões do papel alteradas", cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return Ok(CustomRoleResponse.From(role));
    }

    /// <summary>
    /// Remove um papel personalizado. Os usuários que o utilizavam voltam ao
    /// papel predefinido; nenhum cadastro ou histórico é apagado.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission(Permission.ManageTeam)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var role = await LoadAsync(id, cancellationToken);

        var users = await context.Users
            .Where(user => user.CustomRoleId == id)
            .ToListAsync(cancellationToken);

        foreach (var user in users)
        {
            user.ClearCustomRole();
        }

        // Usuários voltam ao papel predefinido: reemite os tokens deles.
        await RevokeTokensAsync(
            users.Select(user => user.Id), "Papel personalizado excluído", cancellationToken);

        context.CustomRoles.Remove(role);
        await context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>Atribui um papel personalizado a um funcionário.</summary>
    [HttpPut("{id:guid}/users/{userId:guid}")]
    [RequirePermission(Permission.ManageTeam)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var role = await LoadAsync(id, cancellationToken);

        // Impede auto-atribuição: ninguém altera as próprias permissões.
        if (userId == ActorUserId())
        {
            throw new DomainRuleException(
                "Você não pode atribuir um papel personalizado a si mesmo.");
        }

        // Impede escalonamento: o ator só atribui papéis cujas permissões possui.
        RolePermissions.EnsureCanGrant(ActorPermissions(), role.Permissions);

        var user = await context.Users
            .FirstOrDefaultAsync(item => item.Id == userId, cancellationToken)
            ?? throw NotFoundException.For("Usuário", userId);

        user.AssignCustomRole(role.Id);

        // Permissões efetivas mudaram: reemite os tokens do usuário.
        await RevokeTokensAsync(
            [user.Id], "Papel personalizado atribuído", cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>Remove o papel personalizado de um funcionário.</summary>
    [HttpDelete("{id:guid}/users/{userId:guid}")]
    [RequirePermission(Permission.ManageTeam)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnassignAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await LoadAsync(id, cancellationToken);

        var user = await context.Users
            .FirstOrDefaultAsync(item => item.Id == userId, cancellationToken)
            ?? throw NotFoundException.For("Usuário", userId);

        if (user.CustomRoleId == id)
        {
            user.ClearCustomRole();

            await RevokeTokensAsync(
                [user.Id], "Papel personalizado removido", cancellationToken);

            await context.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    private async Task<CustomRole> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();

        return await context.CustomRoles
            .FirstOrDefaultAsync(role => role.Id == id && role.TenantId == tenantId, cancellationToken)
            ?? throw NotFoundException.For("Papel", id);
    }

    private static IEnumerable<Permission> ParsePermissions(IReadOnlyList<string> permissions)
    {
        foreach (var value in permissions)
        {
            if (!Enum.TryParse<Permission>(value, ignoreCase: true, out var permission))
            {
                throw new DomainRuleException($"A permissão '{value}' não existe.");
            }

            yield return permission;
        }
    }

    private Guid RequireTenant() =>
        tenantContext.TenantId ?? throw new DomainRuleException("Estabelecimento não identificado.");
}

/// <summary>Corpo de criação de papel personalizado.</summary>
public sealed record CreateCustomRoleRequest(
    [Required, MaxLength(100)] string Name,
    IReadOnlyList<string> Permissions);

/// <summary>Corpo de alteração de papel personalizado.</summary>
public sealed record UpdateCustomRoleRequest(
    [Required, MaxLength(100)] string Name,
    IReadOnlyList<string> Permissions);

/// <summary>Representação de papel personalizado.</summary>
public sealed record CustomRoleResponse(Guid Id, string Name, IReadOnlyList<string> Permissions)
{
    public static CustomRoleResponse From(CustomRole role) =>
        new(role.Id, role.Name,
            role.Permissions.OrderBy(permission => permission.ToString())
                .Select(permission => permission.ToString())
                .ToList());
}
