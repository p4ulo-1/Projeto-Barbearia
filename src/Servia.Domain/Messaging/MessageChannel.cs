namespace Servia.Domain.Messaging;

/// <summary>
/// Canal de comunicação com o cliente.
/// </summary>
public enum MessageChannel
{
    /// <summary>WhatsApp, via Cloud API da Meta.</summary>
    WhatsApp = 1,

    /// <summary>E-mail.</summary>
    Email = 2,

    /// <summary>SMS, opcional.</summary>
    Sms = 3
}

/// <summary>
/// Tipo de mensagem enviada ao cliente (F6.13).
///
/// O tipo define a permissão: o estabelecimento decide quais tipos podem ser
/// enviados, e o servidor bloqueia o que não estiver autorizado.
/// </summary>
public enum MessageType
{
    /// <summary>Confirmação de agendamento (F6.3).</summary>
    AppointmentConfirmation = 1,

    /// <summary>Lembrete antes do horário (F6.3).</summary>
    AppointmentReminder = 2,

    /// <summary>Aviso de remarcação ou cancelamento (F6.4).</summary>
    AppointmentChange = 3,

    /// <summary>Resposta automática no atendimento (F6.5).</summary>
    AutomatedReply = 4,

    /// <summary>Mensagem de campanha ou relacionamento.</summary>
    Marketing = 5,

    /// <summary>Mensagem escrita por um membro da equipe.</summary>
    Manual = 6
}

/// <summary>Direção da mensagem em uma conversa.</summary>
public enum MessageDirection
{
    /// <summary>Enviada pelo estabelecimento ao cliente.</summary>
    Outbound = 1,

    /// <summary>Recebida do cliente.</summary>
    Inbound = 2
}

/// <summary>Estado de uma notificação.</summary>
public enum NotificationStatus
{
    /// <summary>Agendada para envio futuro.</summary>
    Scheduled = 1,

    /// <summary>Enviada com sucesso.</summary>
    Sent = 2,

    /// <summary>Falha no envio.</summary>
    Failed = 3,

    /// <summary>Cancelada antes do envio.</summary>
    Cancelled = 4
}

/// <summary>Estado de uma conversa de atendimento.</summary>
public enum ConversationStatus
{
    /// <summary>Atendimento automatizado em andamento.</summary>
    Automated = 1,

    /// <summary>Escalada para um membro da equipe (F6.7).</summary>
    WithHuman = 2,

    /// <summary>Encerrada.</summary>
    Closed = 3
}
