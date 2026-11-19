using Servia.Application.Webhooks;
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

    // ---------- F17.4: idempotencia ----------

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
    private readonly List<Conversation> _conversations = [];

    public IReadOnlyList<Conversation> Conversations => _conversations;

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
