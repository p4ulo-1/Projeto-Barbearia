using Servia.Domain.Ai;
using Servia.Domain.Common;
using Servia.Domain.Sites;
using Servia.Domain.Tenancy;

namespace Servia.Tests.Onboarding;

/// <summary>
/// Regras de domínio do onboarding assistido por IA (itens 1 e 2).
///
/// Cobrem os estados, a preservação do histórico e os guardrails que
/// impedem publicar sem aprovação ou aplicar componente fora do schema.
/// </summary>
public sealed class OnboardingDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static OnboardingSession Start() =>
        OnboardingSession.Start(Guid.NewGuid(), "Barbearia Exemplo", SegmentType.Barbershop, Now);

    [Fact]
    public void Sessao_Nasce_Coletando()
    {
        var session = Start();

        Assert.Equal(OnboardingStatus.Collecting, session.Status);
        Assert.True(session.IsOpen);
        Assert.Empty(session.Messages);
    }

    [Fact]
    public void Registro_De_Resposta_Preserva_Historico()
    {
        var session = Start();

        session.AskQuestion("Qual o endereço?", Now);
        session.RecordAnswer("Rua A, 100", new Dictionary<string, string>(), Now);

        Assert.Equal(2, session.Messages.Count);
        Assert.Equal(AiConversationRole.Assistant, session.Messages.First().Role);
        Assert.Equal(AiConversationRole.Owner, session.Messages.Last().Role);
    }

    [Fact]
    public void RecordFacts_Nao_Duplica_Mensagem_E_Acumula_Fatos()
    {
        var session = Start();

        session.RecordAnswer("Rua A, 100; (11) 9999-0000", new Dictionary<string, string>(), Now);
        session.RecordFacts(new Dictionary<string, string>
        {
            ["address"] = "Rua A, 100",
            ["phone"] = "(11) 9999-0000"
        }, Now);

        // A resposta foi registrada uma vez; os fatos não criam nova mensagem.
        Assert.Single(session.Messages);
        Assert.Equal("Rua A, 100", session.CollectedFacts["address"]);
        Assert.Equal("(11) 9999-0000", session.CollectedFacts["phone"]);
    }

    [Fact]
    public void Fato_Nao_Sobrescreve_Valor_Existente_Com_Vazio()
    {
        var session = Start();

        session.RecordFacts(new Dictionary<string, string> { ["address"] = "Rua A" }, Now);
        session.RecordFacts(new Dictionary<string, string> { ["address"] = "" }, Now);

        Assert.Equal("Rua A", session.CollectedFacts["address"]);
    }

    [Fact]
    public void Geracao_Exige_Estado_De_Coleta()
    {
        var session = Start();
        var changeSetId = Guid.NewGuid();

        session.AttachGeneratedConfiguration(changeSetId, Now);

        Assert.Equal(OnboardingStatus.Reviewing, session.Status);
        Assert.Equal(changeSetId, session.GeneratedChangeSetId);

        Assert.Throws<DomainRuleException>(() =>
            session.AttachGeneratedConfiguration(Guid.NewGuid(), Now));
    }

    [Fact]
    public void Publicar_Sem_Aprovacao_E_Rejeitado()
    {
        var site = Site.Create(Guid.NewGuid(), "barbearia-exemplo");

        // O rascunho precisa de uma secao completa para publicar, mas o
        // dominio de publicacao do site nao conhece o estado do onboarding —
        // a barreira esta no OnboardingSession.
        var session = Start();
        session.RecordFacts(new Dictionary<string, string>(), Now);
        session.AttachGeneratedConfiguration(Guid.NewGuid(), Now);
        session.SubmitForApproval(Now);

        var beforeApprove = session.Status;

        // A transicao para Published exige Approved.
        Assert.Throws<DomainRuleException>(() => session.MarkPublished(Now));
        Assert.Equal(OnboardingStatus.AwaitingApproval, beforeApprove);
        Assert.False(site.IsPublished);
    }

    [Fact]
    public void Fluxo_Completo_Ate_Publicado()
    {
        var session = Start();

        session.AttachGeneratedConfiguration(Guid.NewGuid(), Now);
        session.SubmitForApproval(Now);
        session.Approve(Guid.NewGuid(), Now);
        session.MarkPublished(Now);

        Assert.Equal(OnboardingStatus.Published, session.Status);
        Assert.False(session.IsOpen);
    }

    [Fact]
    public void Aprovacao_Exige_Responsavel()
    {
        var session = Start();
        session.AttachGeneratedConfiguration(Guid.NewGuid(), Now);
        session.SubmitForApproval(Now);

        Assert.Throws<DomainRuleException>(() => session.Approve(Guid.Empty, Now));
    }

    [Fact]
    public void Ajuste_Volta_Para_Revisao_E_Limpa_Aprovacao()
    {
        var session = Start();
        session.AttachGeneratedConfiguration(Guid.NewGuid(), Now);
        session.SubmitForApproval(Now);

        session.RequestAdjustments(Now);

        Assert.Equal(OnboardingStatus.Reviewing, session.Status);
        Assert.Null(session.ApprovedBy);
    }

    [Fact]
    public void Sessao_Fechada_Nao_Aceita_Mensagem()
    {
        var session = Start();
        session.AttachGeneratedConfiguration(Guid.NewGuid(), Now);
        session.SubmitForApproval(Now);
        session.Approve(Guid.NewGuid(), Now);
        session.MarkPublished(Now);

        Assert.Throws<DomainRuleException>(() =>
            session.RecordAnswer("oi", new Dictionary<string, string>(), Now));
    }
}
