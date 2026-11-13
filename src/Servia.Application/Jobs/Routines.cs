using Servia.Domain.Common;
using Servia.Domain.Messaging;
using Servia.Domain.Operations;

namespace Servia.Application.Jobs;

/// <summary>
/// Envia as notificações cujo horário venceu (F19.2).
///
/// A notificação é marcada como enviada ou como falha. Uma falha não
/// impede as demais: o laço continua e o motivo fica registrado.
/// </summary>
public sealed class NotificationDispatchJob(
    IJobRepository repository,
    IMessagingProvider messaging,
    TimeProvider timeProvider) : IScheduledJob
{
    /// <summary>Máximo de notificações por execução.</summary>
    public const int BatchSize = 100;

    public string Name => "notification-dispatch";

    public TimeSpan Interval => TimeSpan.FromMinutes(1);

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var due = await repository.GetDueNotificationsAsync(now, BatchSize, cancellationToken);

        if (due.Count == 0)
        {
            return;
        }

        foreach (var notification in due)
        {
            try
            {
                var result = await messaging.SendTextAsync(
                    notification.Recipient,
                    notification.Body,
                    notification.Id.ToString(),
                    cancellationToken);

                if (result.Success)
                {
                    notification.MarkSent(now);
                }
                else
                {
                    notification.MarkFailed(result.FailureReason ?? "Falha no envio.");
                }
            }
            catch (DomainRuleException exception)
            {
                notification.MarkFailed(exception.Message);
            }
        }

        await repository.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Marca como vencidas as faturas cujo prazo passou (F19.3).
///
/// Não cobra: registra o vencimento. A cobrança é do gateway, acionada
/// pelo webhook. Aqui o estado da fatura passa a refletir a realidade.
/// </summary>
public sealed class InvoiceOverdueJob(
    IJobRepository repository,
    TimeProvider timeProvider) : IScheduledJob
{
    public const int BatchSize = 200;

    public string Name => "invoice-overdue";

    public TimeSpan Interval => TimeSpan.FromHours(6);

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var overdue = await repository.GetOverdueInvoicesAsync(today, BatchSize, cancellationToken);

        if (overdue.Count == 0)
        {
            return;
        }

        foreach (var invoice in overdue)
        {
            try
            {
                invoice.MarkFailed();
            }
            catch (DomainRuleException)
            {
                // Fatura ja em estado terminal: nada a fazer.
            }
        }

        await repository.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Aplica a régua de inadimplência (F19.4).
///
/// Trial expirado vira atraso; assinatura em atraso é marcada como tal.
/// O bloqueio de escrita é consequência do status, aplicado na
/// autorização — não aqui.
/// </summary>
public sealed class DelinquencyJob(
    IJobRepository repository,
    TimeProvider timeProvider) : IScheduledJob
{
    public const int BatchSize = 200;

    public string Name => "delinquency";

    public TimeSpan Interval => TimeSpan.FromHours(6);

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var expiredTrials = await repository.GetExpiredTrialsAsync(today, BatchSize, cancellationToken);

        foreach (var subscription in expiredTrials)
        {
            try
            {
                subscription.MarkPastDue();
            }
            catch (DomainRuleException)
            {
                // Assinatura ja cancelada: nada a fazer.
            }
        }

        if (expiredTrials.Count > 0)
        {
            await repository.SaveChangesAsync(cancellationToken);
        }
    }
}

/// <summary>
/// Libera as travas de agenda expiradas (F19.5).
///
/// Sem isso, uma trava que não foi liberada bloquearia o horário para
/// sempre — o índice único parcial filtra por `IsActive`.
/// </summary>
public sealed class ScheduleLockCleanupJob(
    IJobRepository repository,
    TimeProvider timeProvider) : IScheduledJob
{
    public const int BatchSize = 500;

    public string Name => "schedule-lock-cleanup";

    public TimeSpan Interval => TimeSpan.FromMinutes(5);

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var expired = await repository.GetExpiredLocksAsync(now, BatchSize, cancellationToken);

        if (expired.Count == 0)
        {
            return;
        }

        foreach (var scheduleLock in expired)
        {
            scheduleLock.Release();
        }

        await repository.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Renova os clubes cujo período terminou (F19.6).
///
/// A renovação zera o consumo do período. Assinatura suspensa por
/// inadimplência não renova: continua suspensa até a regularização.
/// </summary>
public sealed class MembershipRenewalJob(
    IJobRepository repository,
    TimeProvider timeProvider) : IScheduledJob
{
    public const int BatchSize = 200;

    public string Name => "membership-renewal";

    public TimeSpan Interval => TimeSpan.FromHours(12);

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var expired = await repository.GetExpiredMembershipsAsync(today, BatchSize, cancellationToken);

        if (expired.Count == 0)
        {
            return;
        }

        foreach (var membership in expired)
        {
            if (!membership.IsActive)
            {
                // Suspensa ou cancelada: nao renova.
                continue;
            }

            try
            {
                membership.Renew(today, today.AddMonths(1));
            }
            catch (DomainRuleException)
            {
                // Assinatura cancelada: nao renova.
            }
        }

        await repository.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Remove os eventos de webhook fora da janela de retenção (F19.8).
///
/// A idempotência só precisa cobrir a janela em que o provedor reenvia.
/// Manter indefinidamente faria a tabela crescer sem limite.
/// </summary>
public sealed class ProcessedEventRetentionJob(
    IJobRepository repository,
    TimeProvider timeProvider) : IScheduledJob
{
    public const int BatchSize = 1000;

    public string Name => "processed-event-retention";

    public TimeSpan Interval => TimeSpan.FromHours(24);

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var expired = await repository.GetExpiredProcessedEventsAsync(now, BatchSize, cancellationToken);

        if (expired.Count == 0)
        {
            return;
        }

        repository.RemoveProcessedEvents(expired);
        await repository.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Remove contadores de tentativas de agendamento cuja janela expirou (C3).
///
/// O limite em si não depende da limpeza (a janela fixa já reinicia a
/// contagem), mas sem remover as linhas antigas a tabela cresceria sem
/// limite. Roda com folga: só apaga janelas encerradas há mais de um dia.
/// </summary>
public sealed class PublicBookingAttemptCleanupJob(
    IJobRepository repository,
    TimeProvider timeProvider) : IScheduledJob
{
    public string Name => "public-booking-attempt-cleanup";

    public TimeSpan Interval => TimeSpan.FromHours(6);

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var threshold = timeProvider.GetUtcNow().AddDays(-1);

        await repository.RemoveExpiredBookingAttemptsAsync(threshold, cancellationToken);
    }
}
