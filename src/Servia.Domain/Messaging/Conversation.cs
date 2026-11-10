using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Messaging;

/// <summary>
/// Conversa de WhatsApp com um cliente (F6.5–F6.8).
///
/// Agregado raiz do atendimento: agrupa as mensagens trocadas e controla o
/// estado da conversa.
///
/// O ciclo é: automatizada, com humano, encerrada. A escalada para humano
/// (F6.7) é explícita — a conversa não muda de estado por acidente.
///
/// O histórico (F6.8) é a própria coleção de mensagens, vinculada ao cliente.
/// </summary>
public sealed class Conversation : ITenantScoped
{
    private readonly List<Message> _messages = [];

    private Conversation(
        Guid id,
        Guid tenantId,
        Guid? customerId,
        string phoneNumber,
        DateTimeOffset startedAt)
    {
        Id = id;
        TenantId = tenantId;
        CustomerId = customerId;
        PhoneNumber = phoneNumber;
        Status = ConversationStatus.Automated;
        StartedAt = startedAt;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Start"/>.
    /// </summary>
    private Conversation()
    {
        PhoneNumber = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>Cliente vinculado, quando identificado (F6.8).</summary>
    public Guid? CustomerId { get; private set; }

    public string PhoneNumber { get; private set; }

    public ConversationStatus Status { get; private set; }

    /// <summary>Membro da equipe responsável, quando escalada (F6.7).</summary>
    public Guid? AssignedTo { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public IReadOnlyCollection<Message> Messages => _messages;

    /// <summary>Indica se a conversa está aberta.</summary>
    public bool IsOpen => Status != ConversationStatus.Closed;

    /// <summary>Indica se a conversa está com um humano.</summary>
    public bool IsWithHuman => Status == ConversationStatus.WithHuman;

    public static Conversation Start(
        Guid tenantId,
        string phoneNumber,
        DateTimeOffset now,
        Guid? customerId = null)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            throw new DomainRuleException("O telefone é obrigatório.");
        }

        return new Conversation(Guid.NewGuid(), tenantId, customerId, phoneNumber.Trim(), now);
    }

    /// <summary>Registra uma mensagem recebida do cliente.</summary>
    public Message ReceiveMessage(string body, DateTimeOffset now, string? externalId = null)
    {
        EnsureOpen();

        var message = Message.Create(Id, MessageDirection.Inbound, body, now, externalId);
        _messages.Add(message);

        return message;
    }

    /// <summary>Registra uma mensagem enviada ao cliente.</summary>
    public Message SendMessage(string body, DateTimeOffset now, string? externalId = null)
    {
        EnsureOpen();

        var message = Message.Create(Id, MessageDirection.Outbound, body, now, externalId);
        _messages.Add(message);

        return message;
    }

    /// <summary>
    /// Escala a conversa para um membro da equipe (F6.7).
    /// </summary>
    public void EscalateToHuman(Guid userId)
    {
        EnsureOpen();

        if (userId == Guid.Empty)
        {
            throw new DomainRuleException("O usuário é obrigatório.");
        }

        if (Status == ConversationStatus.WithHuman)
        {
            throw new DomainRuleException("A conversa já está com um atendente.");
        }

        Status = ConversationStatus.WithHuman;
        AssignedTo = userId;
    }

    /// <summary>Vincula a conversa a um cliente identificado (F6.8).</summary>
    public void LinkCustomer(Guid customerId)
    {
        if (customerId == Guid.Empty)
        {
            throw new DomainRuleException("O cliente é obrigatório.");
        }

        CustomerId = customerId;
    }

    /// <summary>Encerra a conversa.</summary>
    public void Close(DateTimeOffset now)
    {
        EnsureOpen();

        Status = ConversationStatus.Closed;
        ClosedAt = now;
    }

    private void EnsureOpen()
    {
        if (!IsOpen)
        {
            throw new DomainRuleException("A conversa está encerrada.");
        }
    }
}

/// <summary>Mensagem trocada em uma conversa.</summary>
public sealed class Message
{
    private Message(
        Guid id,
        Guid conversationId,
        MessageDirection direction,
        string body,
        DateTimeOffset sentAt,
        string? externalId)
    {
        Id = id;
        ConversationId = conversationId;
        Direction = direction;
        Body = body;
        SentAt = sentAt;
        ExternalId = externalId;
    }

    public Guid Id { get; private set; }

    public Guid ConversationId { get; private set; }

    public MessageDirection Direction { get; private set; }

    public string Body { get; private set; }

    public DateTimeOffset SentAt { get; private set; }

    /// <summary>Identificador da mensagem no provedor.</summary>
    public string? ExternalId { get; private set; }

    /// <summary>Indica se a mensagem foi enviada pelo estabelecimento.</summary>
    public bool IsOutbound => Direction == MessageDirection.Outbound;

    internal static Message Create(
        Guid conversationId,
        MessageDirection direction,
        string body,
        DateTimeOffset now,
        string? externalId)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new DomainRuleException("O corpo da mensagem é obrigatório.");
        }

        return new Message(
            Guid.NewGuid(), conversationId, direction, body.Trim(), now,
            string.IsNullOrWhiteSpace(externalId) ? null : externalId.Trim());
    }
}
