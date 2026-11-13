using Servia.Application.Jobs;
using Servia.Domain.Appointments;
using Servia.Domain.Billing;
using Servia.Domain.Common;
using Servia.Domain.Memberships;
using Servia.Domain.Messaging;
using Servia.Domain.Operations;
using Servia.Domain.Tenancy;
using Servia.Tests.Common;

namespace Servia.Tests.Jobs;

/// <summary>
/// F19 — rotinas periódicas.
///
/// Cada rotina é testada com tempo controlado: o teste decide quando o
/// prazo venceu, sem esperar o relógio real.
/// </summary>
public class JobTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 11);

    private readonly FakeJobRepository _repository = new();
    private readonly FakeMessagingProvider _messaging = new();

    // ---------- F19.1: executor ----------

    [Fact]
    public async Task Executor_Roda_Rotina_Vencida()
    {
        var job = new CountingJob("teste", TimeSpan.FromMinutes(1));
        var runner = new JobRunner([job]);

        var executions = await runner.RunDueAsync(Now, new Dictionary<string, DateTimeOffset>());

        Assert.Single(executions);
        Assert.True(executions[0].Succeeded);
        Assert.Equal(1, job.RunCount);
    }

    [Fact]
    public async Task Executor_Nao_Roda_Rotina_Antes_Do_Intervalo()
    {
        var job = new CountingJob("teste", TimeSpan.FromHours(1));
        var runner = new JobRunner([job]);

        var lastRuns = new Dictionary<string, DateTimeOffset> { ["teste"] = Now.AddMinutes(-10) };

        var executions = await runner.RunDueAsync(Now, lastRuns);

        Assert.Empty(executions);
        Assert.Equal(0, job.RunCount);
    }

    [Fact]
    public async Task Falha_Em_Uma_Rotina_Nao_Impede_As_Demais()
    {
        var failing = new FailingJob("falha");
        var working = new CountingJob("ok", TimeSpan.FromMinutes(1));
        var runner = new JobRunner([failing, working]);

        var executions = await runner.RunDueAsync(Now, new Dictionary<string, DateTimeOffset>());

        Assert.Equal(2, executions.Count);
        Assert.False(executions[0].Succeeded);
        Assert.True(executions[1].Succeeded);
        Assert.Equal(1, working.RunCount);
    }

    // ---------- F19.2: notificações ----------

    [Fact]
    public async Task Notificacao_Vencida_Eh_Enviada()
    {
        var notification = Notification.Create(
            TenantId, MessageChannel.WhatsApp, MessageType.AppointmentReminder,
            "5511999998888", "Lembrete", null, null, Now.AddMinutes(-5));

        _repository.AddNotification(notification);

        await NewNotificationJob().ExecuteAsync();

        Assert.True(notification.IsSent);
    }

    [Fact]
    public async Task Notificacao_Futura_Nao_Eh_Enviada()
    {
        var notification = Notification.Create(
            TenantId, MessageChannel.WhatsApp, MessageType.AppointmentReminder,
            "5511999998888", "Lembrete", null, null, Now.AddHours(2));

        _repository.AddNotification(notification);

        await NewNotificationJob().ExecuteAsync();

        Assert.False(notification.IsSent);
    }

    [Fact]
    public async Task Falha_No_Envio_Marca_Notificacao_Como_Falha()
    {
        var notification = Notification.Create(
            TenantId, MessageChannel.WhatsApp, MessageType.AppointmentReminder,
            "5511999998888", "Lembrete", null, null, Now.AddMinutes(-5));

        _repository.AddNotification(notification);
        _messaging.FailNext("Número inválido");

        await NewNotificationJob().ExecuteAsync();

        Assert.Equal(NotificationStatus.Failed, notification.Status);
        Assert.Equal("Número inválido", notification.FailureReason);
    }

    // ---------- F19.3: faturas vencidas ----------

    [Fact]
    public async Task Fatura_Vencida_Eh_Marcada_Como_Falha()
    {
        var invoice = PlatformInvoice.Create(
            TenantId, Guid.NewGuid(), new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31),
            new DateOnly(2026, 11, 5));

        _repository.AddInvoice(invoice);

        await new InvoiceOverdueJob(_repository, new FixedTimeProvider(Now)).ExecuteAsync();

        Assert.Equal(InvoiceStatus.Failed, invoice.Status);
    }

    [Fact]
    public async Task Fatura_Dentro_Do_Prazo_Nao_Eh_Alterada()
    {
        var invoice = PlatformInvoice.Create(
            TenantId, Guid.NewGuid(), new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30),
            new DateOnly(2026, 12, 10));

        _repository.AddInvoice(invoice);

        await new InvoiceOverdueJob(_repository, new FixedTimeProvider(Now)).ExecuteAsync();

        Assert.Equal(InvoiceStatus.Pending, invoice.Status);
    }

    // ---------- F19.4: inadimplência ----------

    [Fact]
    public async Task Trial_Expirado_Vira_Atraso()
    {
        var subscription = Subscription.StartTrial(
            TenantId, Guid.NewGuid(), Today.AddDays(-20));

        _repository.AddSubscription(subscription);

        await new DelinquencyJob(_repository, new FixedTimeProvider(Now)).ExecuteAsync();

        Assert.Equal(SubscriptionStatus.PastDue, subscription.Status);
    }

    [Fact]
    public async Task Trial_Vigente_Nao_Eh_Alterado()
    {
        var subscription = Subscription.StartTrial(TenantId, Guid.NewGuid(), Today);

        _repository.AddSubscription(subscription);

        await new DelinquencyJob(_repository, new FixedTimeProvider(Now)).ExecuteAsync();

        Assert.Equal(SubscriptionStatus.Trialing, subscription.Status);
    }

    // ---------- F19.5: travas expiradas ----------

    [Fact]
    public async Task Trava_Expirada_Eh_Liberada()
    {
        var scheduleLock = ScheduleLock.Acquire(
            TenantId, Guid.NewGuid(), Today, new TimeOnly(10, 0), 30,
            Now.AddHours(-1), lifetimeMinutes: 5);

        _repository.AddLock(scheduleLock);

        await new ScheduleLockCleanupJob(_repository, new FixedTimeProvider(Now)).ExecuteAsync();

        Assert.False(scheduleLock.IsActive);
    }

    [Fact]
    public async Task Trava_Ativa_Nao_Eh_Liberada()
    {
        var scheduleLock = ScheduleLock.Acquire(
            TenantId, Guid.NewGuid(), Today, new TimeOnly(10, 0), 30, Now);

        _repository.AddLock(scheduleLock);

        await new ScheduleLockCleanupJob(_repository, new FixedTimeProvider(Now)).ExecuteAsync();

        Assert.True(scheduleLock.IsActive);
    }

    // ---------- F19.6: renovação de clube ----------

    [Fact]
    public async Task Clube_Vencido_Eh_Renovado()
    {
        var membership = Membership.Subscribe(
            TenantId, Guid.NewGuid(), Guid.NewGuid(),
            Today.AddMonths(-1), Today.AddDays(-1));

        _repository.AddMembership(membership);

        await new MembershipRenewalJob(_repository, new FixedTimeProvider(Now)).ExecuteAsync();

        Assert.Equal(Today.AddMonths(1), membership.CurrentPeriodEnd);
    }

    [Fact]
    public async Task Clube_Suspenso_Nao_Eh_Renovado()
    {
        var membership = Membership.Subscribe(
            TenantId, Guid.NewGuid(), Guid.NewGuid(),
            Today.AddMonths(-1), Today.AddDays(-1));

        membership.Suspend();
        _repository.AddMembership(membership);

        await new MembershipRenewalJob(_repository, new FixedTimeProvider(Now)).ExecuteAsync();

        Assert.True(membership.IsSuspended);
    }

    // ---------- F19.8: retenção ----------

    [Fact]
    public async Task Evento_Fora_Da_Retencao_Eh_Removido()
    {
        var old = ProcessedEvent.Record(
            TenantId, "evt-antigo", "mercadopago",
            Now.AddDays(-(ProcessedEvent.RetentionDays + 1)));

        var recent = ProcessedEvent.Record(TenantId, "evt-recente", "mercadopago", Now);

        _repository.AddProcessedEvent(old);
        _repository.AddProcessedEvent(recent);

        await new ProcessedEventRetentionJob(_repository, new FixedTimeProvider(Now)).ExecuteAsync();

        Assert.DoesNotContain(old, _repository.ProcessedEvents);
        Assert.Contains(recent, _repository.ProcessedEvents);
    }

    private NotificationDispatchJob NewNotificationJob() =>
        new(_repository, _messaging, new FixedTimeProvider(Now));
}

/// <summary>Rotina que conta execuções.</summary>
internal sealed class CountingJob(string name, TimeSpan interval) : IScheduledJob
{
    public string Name => name;

    public TimeSpan Interval => interval;

    public int RunCount { get; private set; }

    public Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        RunCount++;
        return Task.CompletedTask;
    }
}

/// <summary>Rotina que sempre falha.</summary>
internal sealed class FailingJob(string name) : IScheduledJob
{
    public string Name => name;

    public TimeSpan Interval => TimeSpan.FromMinutes(1);

    public Task ExecuteAsync(CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Falha simulada.");
}

/// <summary>Repositório de jobs em memória.</summary>
internal sealed class FakeJobRepository : IJobRepository
{
    private readonly List<Notification> _notifications = [];
    private readonly List<PlatformInvoice> _invoices = [];
    private readonly List<Subscription> _subscriptions = [];
    private readonly List<ScheduleLock> _locks = [];
    private readonly List<Membership> _memberships = [];
    private readonly List<ProcessedEvent> _processed = [];

    public IReadOnlyList<ProcessedEvent> ProcessedEvents => _processed;

    public void AddNotification(Notification notification) => _notifications.Add(notification);

    public void AddInvoice(PlatformInvoice invoice) => _invoices.Add(invoice);

    public void AddSubscription(Subscription subscription) => _subscriptions.Add(subscription);

    public void AddLock(ScheduleLock scheduleLock) => _locks.Add(scheduleLock);

    public void AddMembership(Membership membership) => _memberships.Add(membership);

    public void AddProcessedEvent(ProcessedEvent processedEvent) => _processed.Add(processedEvent);

    public Task<IReadOnlyList<Notification>> GetDueNotificationsAsync(
        DateTimeOffset now, int limit, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Notification>>(
            _notifications.Where(n => n.IsDue(now)).Take(limit).ToList());

    public Task<IReadOnlyList<PlatformInvoice>> GetOverdueInvoicesAsync(
        DateOnly today, int limit, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PlatformInvoice>>(
            _invoices.Where(i => i.Status == InvoiceStatus.Pending && i.DueDate < today)
                .Take(limit).ToList());

    public Task<IReadOnlyList<Subscription>> GetPastDueSubscriptionsAsync(
        int limit, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Subscription>>(
            _subscriptions.Where(s => s.Status == SubscriptionStatus.PastDue).Take(limit).ToList());

    public Task<IReadOnlyList<Subscription>> GetExpiredTrialsAsync(
        DateOnly today, int limit, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Subscription>>(
            _subscriptions.Where(s => s.Status == SubscriptionStatus.Trialing
                                      && s.TrialEndsAt.HasValue
                                      && s.TrialEndsAt.Value < today)
                .Take(limit).ToList());

    public Task<IReadOnlyList<ScheduleLock>> GetExpiredLocksAsync(
        DateTimeOffset now, int limit, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ScheduleLock>>(
            _locks.Where(l => l.IsActive && l.ExpiresAt <= now).Take(limit).ToList());

    public Task<IReadOnlyList<Membership>> GetExpiredMembershipsAsync(
        DateOnly today, int limit, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Membership>>(
            _memberships.Where(m => m.Status == MembershipStatus.Active
                                    && m.CurrentPeriodEnd < today)
                .Take(limit).ToList());

    public Task<IReadOnlyList<ProcessedEvent>> GetExpiredProcessedEventsAsync(
        DateTimeOffset now, int limit, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ProcessedEvent>>(
            _processed.Where(p => p.IsExpired(now)).Take(limit).ToList());

    public void RemoveProcessedEvents(IEnumerable<ProcessedEvent> events)
    {
        foreach (var processedEvent in events.ToList())
        {
            _processed.Remove(processedEvent);
        }
    }

    public Task<int> RemoveExpiredBookingAttemptsAsync(
        DateTimeOffset before,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(0);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

/// <summary>Provedor de mensageria que pode falhar sob demanda.</summary>
internal sealed class FakeMessagingProvider : IMessagingProvider
{
    private string? _failureReason;

    public string ProviderName => "fake";

    public void FailNext(string reason) => _failureReason = reason;

    public Task<MessageSendResult> SendTextAsync(
        string phoneNumber, string body, string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (_failureReason is not null)
        {
            var reason = _failureReason;
            _failureReason = null;

            return Task.FromResult(MessageSendResult.Failed(reason));
        }

        return Task.FromResult(MessageSendResult.Sent($"wamid.{Guid.NewGuid():N}"));
    }

    public Task<MessageSendResult> SendTemplateAsync(
        string phoneNumber, string templateName,
        IReadOnlyDictionary<string, string> parameters, string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        SendTextAsync(phoneNumber, templateName, idempotencyKey, cancellationToken);

    public Task<bool> IsTemplateApprovedAsync(
        string templateName, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);
}
