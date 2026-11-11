using Microsoft.EntityFrameworkCore;
using Servia.Domain.Operations;
using Servia.Infrastructure.Persistence;

namespace Servia.Infrastructure.Repositories;

/// <summary>
/// Resolve o tenant de um webhook (F17).
///
/// O provedor não conhece o tenant. Ele é descoberto pelo identificador
/// que o próprio Servia registrou ao criar a cobrança ou a conversa —
/// nunca por um header que o cliente controla.
///
/// As consultas usam `IgnoreQueryFilters` porque o webhook chega sem
/// contexto de tenant: é justamente a busca que o descobre. O resultado
/// é apenas o identificador do tenant, nunca dados de negócio.
/// </summary>
public sealed class WebhookTenantResolver(AppDbContext context) : IWebhookTenantResolver
{
    public async Task<Guid?> ResolveByPaymentAsync(
        string gatewayPaymentId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(gatewayPaymentId))
        {
            return null;
        }

        var tenantId = await context.PlatformInvoices
            .IgnoreQueryFilters()
            .Where(invoice => invoice.GatewayPaymentId == gatewayPaymentId)
            .Select(invoice => (Guid?)invoice.TenantId)
            .FirstOrDefaultAsync(cancellationToken);

        return tenantId;
    }

    public async Task<Guid?> ResolveByPhoneNumberAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return null;
        }

        var tenantId = await context.Conversations
            .IgnoreQueryFilters()
            .Where(conversation => conversation.PhoneNumber == phoneNumber)
            .Select(conversation => (Guid?)conversation.TenantId)
            .FirstOrDefaultAsync(cancellationToken);

        return tenantId;
    }
}
