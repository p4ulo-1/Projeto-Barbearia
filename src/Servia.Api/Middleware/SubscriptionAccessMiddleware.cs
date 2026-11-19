using Servia.Application.Billing;
using Servia.Domain.Identity;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Identity;

namespace Servia.Api.Middleware;

public sealed class SubscriptionAccessMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantContext tenant, SubscriptionAccessService billing)
    {
        var request = context.Request;
        if (tenant.TenantId is { } tenantId && request.Path.StartsWithSegments("/api")
            && !request.Path.StartsWithSegments("/api/auth")
            && await billing.GetAsync(tenantId, context.RequestAborted) == BillingAccess.OwnerReadOnly)
        {
            var owner = context.User.IsInRole(nameof(Role.Owner))
                || context.User.HasClaim(JwtTokenIssuer.RoleClaim, nameof(Role.Owner));
            var read = HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method)
                || HttpMethods.IsOptions(request.Method);
            if (!owner || (!read && !request.Path.StartsWithSegments("/api/v1/billing")))
            {
                throw new BillingAccessDeniedException();
            }
        }
        await next(context);
    }
}
