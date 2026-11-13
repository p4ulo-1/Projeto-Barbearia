using Microsoft.AspNetCore.Authorization;
using Servia.Domain.Identity;

namespace Servia.Api.Authorization;

/// <summary>
/// Exige que o usuário autenticado possua a permissão informada (F10.4).
///
/// A verificação acontece <b>no servidor</b>, a partir do papel assinado no
/// token. O cliente não decide o que pode fazer: ele apenas recebe 403 quando
/// tenta algo que seu papel não permite.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    /// <summary>Prefixo da política, usado para distinguir de outras políticas.</summary>
    public const string PolicyPrefix = "permission:";

    public RequirePermissionAttribute(Permission permission)
    {
        Policy = PolicyPrefix + permission;
    }
}

/// <summary>
/// Provedor de políticas de permissão.
///
/// Registra uma política por valor de <see cref="Permission"/>, de modo que
/// <see cref="RequirePermissionAttribute"/> funcione para qualquer permissão
/// sem configuração adicional.
/// </summary>
public static class PermissionPolicyProvider
{
    /// <summary>Registra as políticas de todas as permissões existentes.</summary>
    public static void AddPermissionPolicies(this AuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        foreach (var permission in Enum.GetValues<Permission>())
        {
            options.AddPolicy(
                RequirePermissionAttribute.PolicyPrefix + permission,
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(new PermissionRequirement(permission)));
        }
    }
}

/// <summary>Requisito de uma permissão específica.</summary>
public sealed class PermissionRequirement(Permission permission) : IAuthorizationRequirement
{
    public Permission Permission { get; } = permission;
}

/// <summary>
/// Verifica se o papel do usuário possui a permissão exigida.
///
/// A fonte da verdade é <see cref="RolePermissions"/>: o mesmo mapeamento
/// usado pelo domínio, sem duplicação de regra.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        // Permissões efetivas assinadas no token (papel predefinido ou
        // personalizado). Quando presentes, são a fonte da verdade.
        var permissionClaims = context.User
            .FindAll(Infrastructure.Identity.JwtTokenIssuer.PermissionClaim)
            .Select(claim => claim.Value)
            .ToList();

        if (permissionClaims.Count > 0)
        {
            if (permissionClaims.Contains(requirement.Permission.ToString(), StringComparer.Ordinal))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }

        // Compatibilidade: tokens emitidos antes da claim de permissões.
        var roleClaim = context.User.FindFirst(Infrastructure.Identity.JwtTokenIssuer.RoleClaim)?.Value;

        if (roleClaim is null || !Enum.TryParse<Role>(roleClaim, out var role))
        {
            return Task.CompletedTask;
        }

        if (RolePermissions.Has(role, requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
