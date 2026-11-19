using Microsoft.EntityFrameworkCore;
using Servia.Domain.Operations;
using Servia.Infrastructure.Persistence;

namespace Servia.Infrastructure.Repositories;

/// <summary>
/// Resolve o tenant de um webhook de mensagens pelo telefone da conversa.
///
/// As consultas usam `IgnoreQueryFilters` porque o webhook chega sem
/// contexto de tenant: é justamente a busca que o descobre. O resultado
/// é apenas o identificador do tenant, nunca dados de negócio.
/// </summary>
public sealed class WebhookTenantResolver(AppDbContext context) : IWebhookTenantResolver
{
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
