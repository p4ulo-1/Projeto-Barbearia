using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Messaging;

/// <summary>
/// Notificação a ser enviada ao cliente (F6.3, F6.4).
///
/// Cobre confirmação de agendamento, lembrete antes do horário e aviso de
/// remarcação ou cancelamento. A notificação pode ser agendada para envio
/// futuro — o lembrete é o caso típico.
///
/// O ciclo é: agendada, enviada ou falha. Uma notificação já enviada não
/// pode ser reenviada: isso evitaria mensagens duplicadas ao cliente.
/// </summary>
public sealed class Notification : ITenantScoped
{
    private Notification(
        Guid id,
        Guid tenantId,
        Guid? customerId,
        Guid? appointmentId,
        MessageChannel channel,
        MessageType messageType,
        string recipient,
        string body,
        DateTimeOffset? scheduledAt)
    {
        Id = id;
        TenantId = tenantId;
        CustomerId = customerId;
        AppointmentId = appointmentId;
        Channel = channel;
        MessageType = messageType;
        Recipient = recipient;
        Body = body;
        ScheduledAt = scheduledAt;
        // Toda notificacao nasce agendada: o envio imediato e uma
        // notificacao sem ScheduledAt, pronta para envio no instante atual.
        Status = NotificationStatus.Scheduled;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>.
    /// </summary>
    private Notification()
    {
        Recipient = string.Empty;
        Body = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid? CustomerId { get; private set; }

    /// <summary>Agendamento que originou a notificação, quando houver.</summary>
    public Guid? AppointmentId { get; private set; }

    public MessageChannel Channel { get; private set; }

    public MessageType MessageType { get; private set; }

    /// <summary>Telefone ou e-mail do destinatário.</summary>
    public string Recipient { get; private set; }

    public string Body { get; private set; }

    /// <summary>Instante programado para envio. Nulo significa envio imediato.</summary>
    public DateTimeOffset? ScheduledAt { get; private set; }

    public NotificationStatus Status { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public string? FailureReason { get; private set; }

    /// <summary>Indica se a notificação já foi enviada.</summary>
    public bool IsSent => Status == NotificationStatus.Sent;

    /// <summary>
    /// Indica se a notificação está pronta para envio no instante informado.
    /// </summary>
    public bool IsDue(DateTimeOffset now) =>
        Status == NotificationStatus.Scheduled
        && (!ScheduledAt.HasValue || ScheduledAt.Value <= now);

    public static Notification Create(
        Guid tenantId,
        MessageChannel channel,
        MessageType messageType,
        string recipient,
        string body,
        Guid? customerId = null,
        Guid? appointmentId = null,
        DateTimeOffset? scheduledAt = null)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(recipient))
        {
            throw new DomainRuleException("O destinatário é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new DomainRuleException("O corpo da notificação é obrigatório.");
        }

        return new Notification(
            Guid.NewGuid(), tenantId, customerId, appointmentId, channel, messageType,
            recipient.Trim(), body.Trim(), scheduledAt);
    }

    /// <summary>Marca a notificação como enviada.</summary>
    public void MarkSent(DateTimeOffset now)
    {
        if (Status == NotificationStatus.Sent)
        {
            throw new DomainRuleException("A notificação já foi enviada.");
        }

        if (Status == NotificationStatus.Cancelled)
        {
            throw new DomainRuleException("Uma notificação cancelada não pode ser enviada.");
        }

        Status = NotificationStatus.Sent;
        SentAt = now;
        FailureReason = null;
    }

    /// <summary>Registra falha no envio.</summary>
    public void MarkFailed(string reason)
    {
        if (Status == NotificationStatus.Sent)
        {
            throw new DomainRuleException("Uma notificação enviada não pode falhar.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainRuleException("O motivo da falha é obrigatório.");
        }

        Status = NotificationStatus.Failed;
        FailureReason = reason.Trim();
    }

    /// <summary>Cancela a notificação antes do envio.</summary>
    public void Cancel()
    {
        if (Status == NotificationStatus.Sent)
        {
            throw new DomainRuleException("Uma notificação enviada não pode ser cancelada.");
        }

        Status = NotificationStatus.Cancelled;
    }
}
