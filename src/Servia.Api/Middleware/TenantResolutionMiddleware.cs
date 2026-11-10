using Servia.Domain.Tenancy;

namespace Servia.Api.Middleware;

/// <summary>
/// Resolve o tenant da requisição <b>no servidor</b> (ARCH.md §3.2).
///
/// O tenant nunca é aceito de um valor livre enviado pelo cliente. Nesta
/// etapa ele é resolvido pelo header <c>X-Tenant-Id</c>, que representa o
/// subdomínio ou a claim do JWT — a fonte confiável que a autenticação
/// fornecerá quando estiver implementada.
///
/// Sem tenant resolvido, o contexto fica vazio e o filtro global do EF Core
/// nega acesso a dados de negócio. A requisição não é rejeitada aqui: rotas
/// públicas (como o site do tenant) podem não precisar de tenant.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    /// <summary>Header que identifica o tenant. Substituído por subdomínio/JWT na fase F1.5.</summary>
    public const string TenantHeaderName = "X-Tenant-Id";

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tenantContext);

        tenantContext.Clear();

        if (context.Request.Headers.TryGetValue(TenantHeaderName, out var headerValue)
            && Guid.TryParse(headerValue.ToString(), out var tenantId)
            && tenantId != Guid.Empty)
        {
            tenantContext.SetTenant(tenantId);
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
}

/// <summary>Extensão para registrar o middleware no pipeline.</summary>
public static class TenantResolutionMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app) =>
        app.UseMiddleware<TenantResolutionMiddleware>();
}
