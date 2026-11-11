using Servia.Domain.Appointments;
using Servia.Domain.Billing;
using Servia.Domain.Memberships;
using Servia.Domain.Messaging;

namespace Servia.Domain.Operations;

/// <summary>
/// Contrato de persistência das rotinas periódicas (F19).
///
/// As consultas atravessam tenants: uma rotina processa todos os
/// estabelecimentos. Por isso usam `IgnoreQueryFilters` — o isolamento é
/// garantido porque cada operação atua sobre o registro que já carrega
/// seu próprio `TenantId`.
/// </summary>
public interface IJobRepository
{
    /// <summary>Notificações agendadas cujo horário já venceu (F19.2).</summary>
    Task<IReadOnlyList<Notification>> GetDueNotificationsAsync(
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Faturas vencidas e ainda não pagas (F19.3).</summary>
    Task<IReadOnlyList<PlatformInvoice>> GetOverdueInvoicesAsync(
        DateOnly today,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Assinaturas em atraso para aplicar a régua (F19.4).</summary>
    Task<IReadOnlyList<Domain.Tenancy.Subscription>> GetPastDueSubscriptionsAsync(
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Assinaturas cujo trial expirou (F19.4).</summary>
    Task<IReadOnlyList<Domain.Tenancy.Subscription>> GetExpiredTrialsAsync(
        DateOnly today,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Travas de agenda expiradas e ainda ativas (F19.5).</summary>
    Task<IReadOnlyList<ScheduleLock>> GetExpiredLocksAsync(
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Assinaturas de clube cujo período terminou (F19.6).</summary>
    Task<IReadOnlyList<Membership>> GetExpiredMembershipsAsync(
        DateOnly today,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Eventos processados fora da janela de retenção (F19.8).</summary>
    Task<IReadOnlyList<ProcessedEvent>> GetExpiredProcessedEventsAsync(
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Remove registros de eventos expirados.</summary>
    void RemoveProcessedEvents(IEnumerable<ProcessedEvent> events);

    /// <summary>Persiste as alterações pendentes.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
