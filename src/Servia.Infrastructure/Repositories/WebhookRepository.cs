using Microsoft.EntityFrameworkCore;
using Servia.Domain.Billing;
using Servia.Domain.Messaging;
using Servia.Domain.Operations;
using Servia.Infrastructure.Persistence;

namespace Servia.Infrastructure.Repositories;

/// <summary>
/// Implementação EF Core de <see cref="IWebhookRepository"/> (F17).
///
/// A idempotência é verificada no banco: o índice único em
/// `(TenantId, EventId, Source)` impede que o mesmo evento seja
/// registrado duas vezes, mesmo com requisições concorrentes.
/// </summary>
public sealed class WebhookRepository(AppDbContext context) : IWebhookRepository
{
    public Task<bool> WasProcessedAsync(
        Guid tenantId,
        string eventId,
        string source,
        CancellationToken cancellationToken = default) =>
        context.ProcessedEvents
            .AnyAsync(
                processed => processed.TenantId == tenantId
                             && processed.EventId == eventId
                             && processed.Source == source,
                cancellationToken);

    public async Task AddProcessedEventAsync(
        ProcessedEvent processedEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(processedEvent);

        await context.ProcessedEvents.AddAsync(processedEvent, cancellationToken);
    }

    public Task<PlatformInvoice?> FindInvoiceByGatewayIdAsync(
        Guid tenantId,
        string gatewayPaymentId,
        CancellationToken cancellationToken = default) =>
        context.PlatformInvoices
            .FirstOrDefaultAsync(
                invoice => invoice.TenantId == tenantId
                           && invoice.GatewayPaymentId == gatewayPaymentId,
                cancellationToken);

    public Task<Conversation?> FindOpenConversationAsync(
        Guid tenantId,
        string phoneNumber,
        CancellationToken cancellationToken = default) =>
        context.Conversations
            .FirstOrDefaultAsync(
                conversation => conversation.TenantId == tenantId
                                && conversation.PhoneNumber == phoneNumber
                                && conversation.Status != ConversationStatus.Closed,
                cancellationToken);

    public async Task AddConversationAsync(
        Conversation conversation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        await context.Conversations.AddAsync(conversation, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
