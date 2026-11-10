using Servia.Domain.Common;
using Servia.Domain.Messaging;

namespace Servia.Tests.Messaging;

/// <summary>
/// F6.5 — atendimento automatizado.
/// F6.6 — agendamento por conversa.
/// F6.7 — transferência para humano.
/// F6.8 — histórico de conversas.
/// </summary>
public class ConversationTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private static Conversation NewConversation() =>
        Conversation.Start(TenantId, "5511999998888", Now);

    // ---------- F6.5: atendimento automatizado ----------

    [Fact]
    public void Iniciar_Conversa_Em_Modo_Automatizado()
    {
        var conversation = NewConversation();

        Assert.Equal(ConversationStatus.Automated, conversation.Status);
        Assert.True(conversation.IsOpen);
        Assert.False(conversation.IsWithHuman);
        Assert.Empty(conversation.Messages);
    }

    [Fact]
    public void Iniciar_Conversa_Sem_Tenant_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            Conversation.Start(Guid.Empty, "5511999998888", Now));
    }

    [Fact]
    public void Iniciar_Conversa_Sem_Telefone_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            Conversation.Start(TenantId, "  ", Now));
    }

    // ---------- F6.6: agendamento por conversa ----------

    [Fact]
    public void Registrar_Mensagem_Recebida_Do_Cliente()
    {
        var conversation = NewConversation();

        var message = conversation.ReceiveMessage("Quero agendar um corte", Now);

        Assert.Equal(MessageDirection.Inbound, message.Direction);
        Assert.False(message.IsOutbound);
        Assert.Single(conversation.Messages);
    }

    [Fact]
    public void Registrar_Resposta_Automatica()
    {
        var conversation = NewConversation();

        var message = conversation.SendMessage("Temos horário às 14h.", Now);

        Assert.Equal(MessageDirection.Outbound, message.Direction);
        Assert.True(message.IsOutbound);
    }

    [Fact]
    public void Mensagem_Sem_Corpo_Lanca_Excecao()
    {
        var conversation = NewConversation();

        Assert.Throws<DomainRuleException>(() => conversation.ReceiveMessage("  ", Now));
    }

    // ---------- F6.7: transferencia para humano ----------

    [Fact]
    public void Escalar_Conversa_Para_Humano()
    {
        var conversation = NewConversation();

        conversation.EscalateToHuman(UserId);

        Assert.Equal(ConversationStatus.WithHuman, conversation.Status);
        Assert.True(conversation.IsWithHuman);
        Assert.Equal(UserId, conversation.AssignedTo);
    }

    [Fact]
    public void Escalar_Sem_Usuario_Lanca_Excecao()
    {
        var conversation = NewConversation();

        Assert.Throws<DomainRuleException>(() => conversation.EscalateToHuman(Guid.Empty));
    }

    [Fact]
    public void Escalar_Conversa_Ja_Escalada_Lanca_Excecao()
    {
        var conversation = NewConversation();
        conversation.EscalateToHuman(UserId);

        Assert.Throws<DomainRuleException>(() => conversation.EscalateToHuman(Guid.NewGuid()));
    }

    // ---------- F6.8: historico ----------

    [Fact]
    public void Historico_Mantem_Ordem_Das_Mensagens()
    {
        var conversation = NewConversation();

        conversation.ReceiveMessage("Olá", Now);
        conversation.SendMessage("Olá! Como posso ajudar?", Now.AddMinutes(1));
        conversation.ReceiveMessage("Quero agendar", Now.AddMinutes(2));

        Assert.Equal(3, conversation.Messages.Count);
        Assert.False(conversation.Messages.First().IsOutbound);
        Assert.True(conversation.Messages.Last().IsOutbound == false);
    }

    [Fact]
    public void Vincular_Conversa_Ao_Cliente()
    {
        var conversation = NewConversation();

        conversation.LinkCustomer(CustomerId);

        Assert.Equal(CustomerId, conversation.CustomerId);
    }

    [Fact]
    public void Vincular_Cliente_Vazio_Lanca_Excecao()
    {
        var conversation = NewConversation();

        Assert.Throws<DomainRuleException>(() => conversation.LinkCustomer(Guid.Empty));
    }

    [Fact]
    public void Mensagem_Guarda_Identificador_Externo()
    {
        var conversation = NewConversation();

        var message = conversation.ReceiveMessage("Olá", Now, "wamid.123");

        Assert.Equal("wamid.123", message.ExternalId);
    }

    // ---------- Encerramento ----------

    [Fact]
    public void Encerrar_Conversa()
    {
        var conversation = NewConversation();

        conversation.Close(Now);

        Assert.Equal(ConversationStatus.Closed, conversation.Status);
        Assert.False(conversation.IsOpen);
        Assert.Equal(Now, conversation.ClosedAt);
    }

    [Fact]
    public void Conversa_Encerrada_Nao_Recebe_Mensagem()
    {
        var conversation = NewConversation();
        conversation.Close(Now);

        Assert.Throws<DomainRuleException>(() => conversation.ReceiveMessage("Olá", Now));
    }

    [Fact]
    public void Conversa_Encerrada_Nao_Pode_Ser_Escalada()
    {
        var conversation = NewConversation();
        conversation.Close(Now);

        Assert.Throws<DomainRuleException>(() => conversation.EscalateToHuman(UserId));
    }
}
