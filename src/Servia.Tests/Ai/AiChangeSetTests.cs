using Servia.Domain.Ai;
using Servia.Domain.Common;
using Servia.Domain.Sites;

namespace Servia.Tests.Ai;

/// <summary>
/// F8.3 — proposta de alterações.
/// F8.6 — guardrails: só componentes da biblioteca.
/// </summary>
public class AiChangeSetTests
{
    private static AiChangeSet NewChangeSet() =>
        AiChangeSet.Create(Guid.NewGuid(), "Site clássico para barbearia", SiteTheme.Default);

    [Fact]
    public void Criar_Proposta_Valida()
    {
        var changeSet = NewChangeSet();

        Assert.Equal("Site clássico para barbearia", changeSet.Summary);
        Assert.Empty(changeSet.Sections);
    }

    [Fact]
    public void Criar_Proposta_Sem_Resumo_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            AiChangeSet.Create(Guid.NewGuid(), "  ", SiteTheme.Default));
    }

    // ---------- F8.6: guardrails ----------

    [Fact]
    public void Adicionar_Secao_Com_Componente_Da_Biblioteca()
    {
        var changeSet = NewChangeSet();

        var section = changeSet.AddSection(SiteComponentType.Hero,
            new Dictionary<string, string> { ["title"] = "Barbearia XYZ" });

        Assert.Equal(SiteComponentType.Hero, section.ComponentType);
        Assert.Single(changeSet.Sections);
    }

    [Fact]
    public void Rejeitar_Componente_Fora_Da_Biblioteca()
    {
        var changeSet = NewChangeSet();

        // Valor numérico que não corresponde a nenhum componente do enum.
        Assert.Throws<DomainRuleException>(() =>
            changeSet.AddSection((SiteComponentType)999,
                new Dictionary<string, string> { ["title"] = "X" }));
    }

    [Fact]
    public void Rejeitar_Propriedade_Fora_Do_Schema()
    {
        var changeSet = NewChangeSet();

        Assert.Throws<DomainRuleException>(() =>
            changeSet.AddSection(SiteComponentType.Hero,
                new Dictionary<string, string> { ["codigoArbitrario"] = "<script>" }));
    }

    [Fact]
    public void Rejeitar_Propriedade_De_Outro_Componente()
    {
        var changeSet = NewChangeSet();

        // "address" pertence ao Contact, não ao Hero.
        Assert.Throws<DomainRuleException>(() =>
            changeSet.AddSection(SiteComponentType.Hero,
                new Dictionary<string, string> { ["address"] = "Rua X" }));
    }

    [Fact]
    public void Aceitar_Propriedades_Validas_De_Cada_Componente()
    {
        var changeSet = NewChangeSet();

        changeSet.AddSection(SiteComponentType.Hero,
            new Dictionary<string, string> { ["title"] = "Título", ["subtitle"] = "Subtítulo" });

        changeSet.AddSection(SiteComponentType.Contact,
            new Dictionary<string, string>
            {
                ["title"] = "Contato",
                ["address"] = "Rua X",
                ["phone"] = "21999998888"
            });

        Assert.Equal(2, changeSet.Sections.Count);
    }

    // ---------- Completude ----------

    [Fact]
    public void Proposta_Sem_Secoes_Nao_Esta_Completa()
    {
        Assert.False(NewChangeSet().IsComplete());
    }

    [Fact]
    public void Proposta_Com_Secao_Incompleta_Nao_Esta_Completa()
    {
        var changeSet = NewChangeSet();
        changeSet.AddSection(SiteComponentType.Hero, new Dictionary<string, string>());

        Assert.False(changeSet.IsComplete());
    }

    [Fact]
    public void Proposta_Com_Secoes_Completas_Esta_Pronta()
    {
        var changeSet = NewChangeSet();
        changeSet.AddSection(SiteComponentType.Hero,
            new Dictionary<string, string> { ["title"] = "Barbearia XYZ" });

        Assert.True(changeSet.IsComplete());
    }

    [Fact]
    public void Secoes_Mantem_A_Ordem_De_Adicao()
    {
        var changeSet = NewChangeSet();
        changeSet.AddSection(SiteComponentType.Hero,
            new Dictionary<string, string> { ["title"] = "A" });
        changeSet.AddSection(SiteComponentType.About,
            new Dictionary<string, string> { ["title"] = "B", ["body"] = "C" });

        Assert.Equal(0, changeSet.Sections.First().SortOrder);
        Assert.Equal(1, changeSet.Sections.Last().SortOrder);
    }
}
