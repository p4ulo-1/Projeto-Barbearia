using Servia.Domain.Common;
using Servia.Domain.Messaging;

namespace Servia.Tests.Messaging;

/// <summary>
/// F6.2 — templates de mensagem por tenant, com variáveis.
/// </summary>
public class MessageTemplateTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static MessageTemplate NewTemplate(string body = "Olá {{cliente}}, seu horário é {{data}}.") =>
        MessageTemplate.Create(
            TenantId, "confirmacao", MessageChannel.WhatsApp,
            MessageType.AppointmentConfirmation, body);

    [Fact]
    public void Criar_Template_Valido()
    {
        var template = NewTemplate();

        Assert.Equal("confirmacao", template.Key);
        Assert.Equal(MessageChannel.WhatsApp, template.Channel);
        Assert.Equal(MessageType.AppointmentConfirmation, template.MessageType);
        Assert.True(template.IsActive);
    }

    [Fact]
    public void Criar_Template_Sem_Tenant_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            MessageTemplate.Create(Guid.Empty, "k", MessageChannel.WhatsApp,
                MessageType.Manual, "corpo"));
    }

    [Fact]
    public void Criar_Template_Sem_Chave_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            MessageTemplate.Create(TenantId, "  ", MessageChannel.WhatsApp,
                MessageType.Manual, "corpo"));
    }

    [Fact]
    public void Criar_Template_Sem_Corpo_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            MessageTemplate.Create(TenantId, "k", MessageChannel.WhatsApp,
                MessageType.Manual, "  "));
    }

    // ---------- Variaveis ----------

    [Fact]
    public void Template_Identifica_Suas_Variaveis()
    {
        var template = NewTemplate();

        Assert.Equal(2, template.Variables.Count);
        Assert.Contains("cliente", template.Variables);
        Assert.Contains("data", template.Variables);
    }

    [Fact]
    public void Template_Sem_Variaveis_Nao_Tem_Lista()
    {
        var template = NewTemplate("Mensagem fixa.");

        Assert.Empty(template.Variables);
    }

    [Fact]
    public void Variaveis_Repetidas_Contam_Uma_Vez()
    {
        var template = NewTemplate("{{cliente}}, olá {{cliente}}!");

        Assert.Single(template.Variables);
    }

    // ---------- Renderizacao ----------

    [Fact]
    public void Renderizar_Substitui_Variaveis()
    {
        var template = NewTemplate();

        var rendered = template.Render(new Dictionary<string, string>
        {
            ["cliente"] = "João",
            ["data"] = "10/11 às 14h"
        });

        Assert.Equal("Olá João, seu horário é 10/11 às 14h.", rendered);
    }

    [Fact]
    public void Renderizar_Sem_Variavel_Obrigatoria_Lanca_Excecao()
    {
        var template = NewTemplate();

        var exception = Assert.Throws<DomainRuleException>(() =>
            template.Render(new Dictionary<string, string> { ["cliente"] = "João" }));

        Assert.Contains("data", exception.Message);
    }

    [Fact]
    public void Renderizar_Template_Sem_Variaveis_Retorna_Corpo()
    {
        var template = NewTemplate("Mensagem fixa.");

        var rendered = template.Render(new Dictionary<string, string>());

        Assert.Equal("Mensagem fixa.", rendered);
    }

    // ---------- Operacoes ----------

    [Fact]
    public void Atualizar_Corpo_Altera_Variaveis()
    {
        var template = NewTemplate();

        template.UpdateBody("Olá {{nome}}!");

        Assert.Single(template.Variables);
        Assert.Contains("nome", template.Variables);
    }

    [Fact]
    public void Desativar_E_Reativar_Template()
    {
        var template = NewTemplate();

        template.Deactivate();
        Assert.False(template.IsActive);

        template.Activate();
        Assert.True(template.IsActive);
    }
}
