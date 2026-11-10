using Servia.Domain.Common;
using Servia.Domain.Messaging;

namespace Servia.Tests.Messaging;

/// <summary>
/// F6.3 — confirmação e lembretes.
/// F6.4 — avisos de alteração.
/// </summary>
public class NotificationTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid AppointmentId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private static Notification NewNotification(
        MessageType type = MessageType.AppointmentConfirmation,
        DateTimeOffset? scheduledAt = null) =>
        Notification.Create(
            TenantId, MessageChannel.WhatsApp, type, "5511999998888",
            "Seu horário está confirmado.", CustomerId, AppointmentId, scheduledAt);

    // ---------- F6.3: confirmacao e lembrete ----------

    [Fact]
    public void Criar_Confirmacao_De_Agendamento()
    {
        var notification = NewNotification();

        Assert.Equal(MessageType.AppointmentConfirmation, notification.MessageType);
        Assert.Equal(NotificationStatus.Scheduled, notification.Status);
        Assert.Equal(CustomerId, notification.CustomerId);
        Assert.Equal(AppointmentId, notification.AppointmentId);
    }

    [Fact]
    public void Criar_Lembrete_Agendado_Para_O_Futuro()
    {
        var notification = NewNotification(
            MessageType.AppointmentReminder, Now.AddHours(24));

        Assert.Equal(Now.AddHours(24), notification.ScheduledAt);
        Assert.False(notification.IsDue(Now));
    }

    [Fact]
    public void Lembrete_Fica_Devido_No_Horario()
    {
        var notification = NewNotification(
            MessageType.AppointmentReminder, Now.AddHours(24));

        Assert.True(notification.IsDue(Now.AddHours(24)));
        Assert.True(notification.IsDue(Now.AddHours(25)));
    }

    [Fact]
    public void Notificacao_Sem_Agendamento_Eh_Devida_Imediatamente()
    {
        var notification = NewNotification();

        Assert.True(notification.IsDue(Now));
    }

    // ---------- F6.4: aviso de alteracao ----------

    [Fact]
    public void Criar_Aviso_De_Alteracao()
    {
        var notification = NewNotification(MessageType.AppointmentChange);

        Assert.Equal(MessageType.AppointmentChange, notification.MessageType);
    }

    // ---------- Validacoes ----------

    [Fact]
    public void Criar_Sem_Tenant_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            Notification.Create(Guid.Empty, MessageChannel.WhatsApp,
                MessageType.Manual, "5511999998888", "corpo"));
    }

    [Fact]
    public void Criar_Sem_Destinatario_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            Notification.Create(TenantId, MessageChannel.WhatsApp,
                MessageType.Manual, "  ", "corpo"));
    }

    [Fact]
    public void Criar_Sem_Corpo_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            Notification.Create(TenantId, MessageChannel.WhatsApp,
                MessageType.Manual, "5511999998888", "  "));
    }

    // ---------- Ciclo de vida ----------

    [Fact]
    public void Marcar_Como_Enviada()
    {
        var notification = NewNotification();

        notification.MarkSent(Now);

        Assert.True(notification.IsSent);
        Assert.Equal(Now, notification.SentAt);
    }

    [Fact]
    public void Notificacao_Enviada_Nao_Pode_Ser_Reenviada()
    {
        var notification = NewNotification();
        notification.MarkSent(Now);

        Assert.Throws<DomainRuleException>(() => notification.MarkSent(Now.AddMinutes(1)));
    }

    [Fact]
    public void Notificacao_Enviada_Nao_Fica_Devida()
    {
        var notification = NewNotification();
        notification.MarkSent(Now);

        Assert.False(notification.IsDue(Now.AddHours(1)));
    }

    [Fact]
    public void Registrar_Falha_No_Envio()
    {
        var notification = NewNotification();

        notification.MarkFailed("Número inválido");

        Assert.Equal(NotificationStatus.Failed, notification.Status);
        Assert.Equal("Número inválido", notification.FailureReason);
    }

    [Fact]
    public void Falha_Sem_Motivo_Lanca_Excecao()
    {
        var notification = NewNotification();

        Assert.Throws<DomainRuleException>(() => notification.MarkFailed("  "));
    }

    [Fact]
    public void Cancelar_Notificacao_Antes_Do_Envio()
    {
        var notification = NewNotification();

        notification.Cancel();

        Assert.Equal(NotificationStatus.Cancelled, notification.Status);
        Assert.False(notification.IsDue(Now));
    }

    [Fact]
    public void Notificacao_Cancelada_Nao_Pode_Ser_Enviada()
    {
        var notification = NewNotification();
        notification.Cancel();

        Assert.Throws<DomainRuleException>(() => notification.MarkSent(Now));
    }

    [Fact]
    public void Notificacao_Enviada_Nao_Pode_Ser_Cancelada()
    {
        var notification = NewNotification();
        notification.MarkSent(Now);

        Assert.Throws<DomainRuleException>(() => notification.Cancel());
    }
}
