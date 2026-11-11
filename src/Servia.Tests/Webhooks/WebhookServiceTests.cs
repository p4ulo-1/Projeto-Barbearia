using Servia.Application.Webhooks;
using Servia.Domain.Billing;
using Servia.Domain.Common;
using Servia.Domain.Messaging;
using Servia.Domain.Operations;
using Servia.Tests.Common;

namespace Servia.Tests.Webhooks;

/// <summary>
/// F17 — webhooks.
///
/// Receber eventos dos provedores, com idempotência e tratamento de
/// evento desconhecido. Testes de caso de uso, sem banco.
/// </summary>
public class WebhookServiceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeWebhookRepository _repository = new();

    private WebhookService NewService() => new(_repository, new FixedTimeProvider(Now));

    /// <summary>
    /// Cria uma fatura pendente. O identificador do gateway e atribuido
    /// pelo proprio dominio em MarkPaid; aqui o teste usa a fatura pelo
    /// identificador que o repositorio conhece.
    /// </summary>
    private PlatformInvoice SeedInvoice(string gatewayPaymentId)
    {
        var invoice = PlatformInvoice.Create(
            TenantId, Guid.NewGuid(), new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30),
            new DateOnly(2026, 12, 10));

        _repository.AddInvoice(invoice, gatewayPaymentId);

        return invoice;
    }

    // ---------- F17.1: pagamento ----------

    [Fact]
    public async Task Pagamento_Aprovado_Confirma_A_Fatura()
    {
        var invoice = SeedInvoice("pay-123");

        var result = await NewService().HandlePaymentAsync(
            TenantId, new PaymentWebhookEvent("evt-1", "pay-123", "approved"));

        Assert.Equal(WebhookOutcome.Processed, result.Outcome);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
    }

    [Fact]
    public async Task Pagamento_Rejeitado_Marca_A_Fatura_Como_Falha()
    {
        var invoice = SeedInvoice("pay-456");

        var result = await NewService().HandlePaymentAsync(
            TenantId, new PaymentWebhookEvent("evt-2", "pay-456", "rejected"));

        Assert.Equal(WebhookOutcome.Processed, result.Outcome);
        Assert.Equal(InvoiceStatus.Failed, invoice.Status);
    }

    [Fact]
    public async Task Pagamento_De_Fatura_Desconhecida_Eh_Ignorado()
    {
        var result = await NewService().HandlePaymentAsync(
            TenantId, new PaymentWebhookEvent("evt-3", "pay-inexistente", "approved"));

        Assert.Equal(WebhookOutcome.Ignored, result.Outcome);
    }

    [Fact]
    public async Task Status_Intermediario_Nao_Altera_A_Fatura()
    {
        var invoice = SeedInvoice("pay-789");

        var result = await NewService().HandlePaymentAsync(
            TenantId, new PaymentWebhookEvent("evt-4", "pay-789", "pending"));

        Assert.Equal(WebhookOutcome.Ignored, result.Outcome);
        Assert.Equal(InvoiceStatus.Pending, invoice.Status);
    }

    // ---------- F17.4: idempotencia ----------

    [Fact]
    public async Task Evento_Repetido_Nao_Processa_De_Novo()
    {
        SeedInvoice("pay-dup");
        var service = NewService();

        var first = await service.HandlePaymentAsync(
            TenantId, new PaymentWebhookEvent("evt-dup", "pay-dup", "approved"));

        var second = await service.HandlePaymentAsync(
            TenantId, new PaymentWebhookEvent("evt-dup", "pay-dup", "approved"));

        Assert.Equal(WebhookOutcome.Processed, first.Outcome);
        Assert.Equal(WebhookOutcome.Duplicate, second.Outcome);
    }

    [Fact]
    public async Task Evento_Repetido_Nao_Gera_Mensagem_Duplicada()
    {
        var service = NewService();
        var payload = new IncomingMessageWebhookEvent("evt-msg", "5511999998888", "Olá", "wamid.1");

        await service.HandleIncomingMessageAsync(TenantId, payload);
        await service.HandleIncomingMessageAsync(TenantId, payload);

        var conversation = _repository.Conversations.Single();

        Assert.Single(conversation.Messages);
    }

    // ---------- F17.2: mensagem recebida ----------

    [Fact]
    public async Task Mensagem_Recebida_Cria_Conversa()
    {
        var result = await NewService().HandleIncomingMessageAsync(
            TenantId, new IncomingMessageWebhookEvent("evt-5", "5511999998888", "Quero agendar", "wamid.5"));

        Assert.Equal(WebhookOutcome.Processed, result.Outcome);

        var conversation = _repository.Conversations.Single();
        Assert.Equal("5511999998888", conversation.PhoneNumber);
        Assert.Single(conversation.Messages);
    }

    [Fact]
    public async Task Segunda_Mensagem_Entra_Na_Conversa_Existente()
    {
        var service = NewService();

        await service.HandleIncomingMessageAsync(
            TenantId, new IncomingMessageWebhookEvent("evt-6", "5511999998888", "Olá", null));

        await service.HandleIncomingMessageAsync(
            TenantId, new IncomingMessageWebhookEvent("evt-7", "5511999998888", "Quero agendar", null));

        var conversation = _repository.Conversations.Single();
        Assert.Equal(2, conversation.Messages.Count);
    }

    [Fact]
    public async Task Mensagem_Sem_Corpo_Eh_Ignorada()
    {
        var result = await NewService().HandleIncomingMessageAsync(
            TenantId, new IncomingMessageWebhookEvent("evt-8", "5511999998888", "  ", null));

        Assert.Equal(WebhookOutcome.Ignored, result.Outcome);
        Assert.Empty(_repository.Conversations);
    }

    // ---------- F17.5: evento desconhecido ----------

    [Fact]
    public async Task Evento_Sem_Identificador_Eh_Rejeitado()
    {
        var result = await NewService().HandlePaymentAsync(
            TenantId, new PaymentWebhookEvent("  ", "pay-1", "approved"));

        Assert.Equal(WebhookOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public async Task Evento_Sem_Tenant_Eh_Rejeitado()
    {
        var result = await NewService().HandlePaymentAsync(
            Guid.Empty, new PaymentWebhookEvent("evt-9", "pay-1", "approved"));

        Assert.Equal(WebhookOutcome.Rejected, result.Outcome);
    }

    // ---------- F17.4: entidade ----------

    [Fact]
    public void Evento_Processado_Expira_Apos_A_Retencao()
    {
        var processed = ProcessedEvent.Record(TenantId, "evt-1", "mercadopago", Now);

        Assert.False(processed.IsExpired(Now.AddDays(ProcessedEvent.RetentionDays - 1)));
        Assert.True(processed.IsExpired(Now.AddDays(ProcessedEvent.RetentionDays + 1)));
    }

    [Fact]
    public void Evento_Processado_Sem_Origem_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            ProcessedEvent.Record(TenantId, "evt-1", "  ", Now));
    }
}

/// <summary>Repositório de webhooks em memória.</summary>
internal sealed class FakeWebhookRepository : IWebhookRepository
{
    private readonly List<ProcessedEvent> _processed = [];
    private readonly List<PlatformInvoice> _invoices = [];
    private readonly List<Conversation> _conversations = [];

    public IReadOnlyList<Conversation> Conversations => _conversations;

    private readonly Dictionary<string, PlatformInvoice> _byGatewayId = [];

    public void AddInvoice(PlatformInvoice invoice, string gatewayPaymentId)
    {
        _invoices.Add(invoice);
        _byGatewayId[gatewayPaymentId] = invoice;
    }

    public Task<bool> WasProcessedAsync(
        Guid tenantId, string eventId, string source, CancellationToken cancellationToken = default) =>
        Task.FromResult(_processed.Any(
            processed => processed.TenantId == tenantId
                         && processed.EventId == eventId
                         && processed.Source == source));

    public Task AddProcessedEventAsync(
        ProcessedEvent processedEvent, CancellationToken cancellationToken = default)
    {
        _processed.Add(processedEvent);
        return Task.CompletedTask;
    }

    public Task<PlatformInvoice?> FindInvoiceByGatewayIdAsync(
        Guid tenantId, string gatewayPaymentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_byGatewayId.TryGetValue(gatewayPaymentId, out var invoice)
            && invoice.TenantId == tenantId
                ? invoice
                : null);

    public Task<Conversation?> FindOpenConversationAsync(
        Guid tenantId, string phoneNumber, CancellationToken cancellationToken = default) =>
        Task.FromResult(_conversations.FirstOrDefault(
            conversation => conversation.TenantId == tenantId
                            && conversation.PhoneNumber == phoneNumber
                            && conversation.Status != ConversationStatus.Closed));

    public Task AddConversationAsync(
        Conversation conversation, CancellationToken cancellationToken = default)
    {
        _conversations.Add(conversation);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
