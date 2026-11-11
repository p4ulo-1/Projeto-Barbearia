using Servia.Domain.Tenancy;
using Servia.Infrastructure.Identity;

namespace Servia.Api.Middleware;

/// <summary>
/// Resolve o tenant da requisição <b>exclusivamente</b> a partir da
/// identidade autenticada (Etapa 3 da auditoria).
///
/// A única fonte é a claim <c>tenant_id</c> do token, assinada pelo
/// servidor. O cliente não informa o tenant: ele vem do token.
///
/// <b>O que foi removido:</b> o fallback pelo header <c>X-Tenant-Id</c>.
/// Ele permitia que qualquer cliente escolhesse o estabelecimento — em
/// Development, sem sequer autenticar.
///
/// <b>Administrador da plataforma:</b> não pertence a um tenant. Para
/// operar sobre um estabelecimento específico, usa o endpoint
/// administrativo explícito, que valida o papel e registra a ação em
/// auditoria. O header não é aceito em nenhum caso.
///
/// Sem tenant resolvido, o contexto fica vazio e o filtro global do EF
/// Core nega acesso a dados de negócio.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tenantContext);

        tenantContext.Clear();

        var tenantId = ResolveFromToken(context);

        if (tenantId.HasValue)
        {
            tenantContext.SetTenant(tenantId.Value);
        }

        try
        {
            await next(context);
        }
        finally
        {
            // Limpa ao final para não vazar o tenant entre requisições.
            tenantContext.Clear();
        }
    }

    /// <summary>
    /// Lê o tenant da claim do token. É a única fonte aceita.
    /// </summary>
    private static Guid? ResolveFromToken(HttpContext context)
    {
        var claim = context.User.FindFirst(JwtTokenIssuer.TenantIdClaim)?.Value;

        return Guid.TryParse(claim, out var tenantId) && tenantId != Guid.Empty
            ? tenantId
            : null;
    }
}

/// <summary>Extensão para registrar o middleware no pipeline.</summary>
public static class TenantResolutionMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app) =>
        app.UseMiddleware<TenantResolutionMiddleware>();
}
