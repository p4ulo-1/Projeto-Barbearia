using Servia.Domain.Tenancy;
using Servia.Infrastructure.Identity;

namespace Servia.Api.Middleware;

/// <summary>
/// Resolve o tenant da requisição <b>no servidor</b> (F10.5).
///
/// A ordem de precedência é deliberada:
///
///   1. <b>Claim do token</b> — fonte confiável, assinada pelo servidor.
///   2. <b>Header <c>X-Tenant-Id</c></b> — apenas em desenvolvimento, para
///      rotas públicas e testes. Em produção é ignorado.
///
/// Aceitar o header em produção permitiria a um cliente escolher o tenant e
/// acessar dados de outro estabelecimento.
///
/// Sem tenant resolvido, o contexto fica vazio e o filtro global do EF Core
/// nega acesso a dados de negócio.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next, IHostEnvironment environment)
{
    /// <summary>Header de desenvolvimento. Ignorado em produção.</summary>
    public const string TenantHeaderName = "X-Tenant-Id";

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tenantContext);

        tenantContext.Clear();

        var tenantId = ResolveFromToken(context) ?? ResolveFromHeader(context);

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
    /// Lê o tenant da claim do token. É a fonte confiável.
    /// </summary>
    private static Guid? ResolveFromToken(HttpContext context)
    {
        var claim = context.User.FindFirst(JwtTokenIssuer.TenantIdClaim)?.Value;

        return Guid.TryParse(claim, out var tenantId) && tenantId != Guid.Empty
            ? tenantId
            : null;
    }

    /// <summary>
    /// Lê o tenant do header. Aceito apenas fora de produção.
    /// </summary>
    private Guid? ResolveFromHeader(HttpContext context)
    {
        if (!environment.IsDevelopment())
        {
            return null;
        }

        if (!context.Request.Headers.TryGetValue(TenantHeaderName, out var headerValue))
        {
            return null;
        }

        return Guid.TryParse(headerValue.ToString(), out var tenantId) && tenantId != Guid.Empty
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
