using Servia.Domain.Common;
using Servia.Domain.Sites;

namespace Servia.Tests.Sites;

/// <summary>
/// F3.2 — biblioteca de componentes com configuração validada.
/// F3.4 — editor de conteúdo.
/// F3.5 — prévia multi-dispositivo.
/// F3.8 — agendamento embutido no site.
/// </summary>
public class SiteComponentTests
{
    // ---------- F3.2: schema dos componentes ----------

    [Fact]
    public void Todos_Os_Componentes_Tem_Schema()
    {
        foreach (var componentType in Enum.GetValues<SiteComponentType>())
        {
            Assert.True(SiteComponentSchema.IsSupported(componentType),
                $"O componente {componentType} não tem schema.");
            Assert.NotEmpty(SiteComponentSchema.AllowedKeys(componentType));
        }
    }

    [Fact]
    public void Hero_Exige_Titulo()
    {
        var required = SiteComponentSchema.RequiredKeys(SiteComponentType.Hero);

        Assert.Contains("title", required);
    }

    [Fact]
    public void Contact_Exige_Endereco_E_Telefone()
    {
        var required = SiteComponentSchema.RequiredKeys(SiteComponentType.Contact);

        Assert.Contains("address", required);
        Assert.Contains("phone", required);
    }

    // ---------- F3.4: editor de conteudo ----------

    [Fact]
    public void Definir_Propriedade_Valida_Do_Componente()
    {
        var section = SiteSection.Create(SiteComponentType.Hero);

        section.SetProperty("title", "Barbearia XYZ");

        Assert.Equal("Barbearia XYZ", section.Configuration["title"]);
    }

    [Fact]
    public void Definir_Propriedade_Invalida_Lanca_Excecao()
    {
        var section = SiteSection.Create(SiteComponentType.Hero);

        Assert.Throws<DomainRuleException>(() =>
            section.SetProperty("corInexistente", "valor"));
    }

    [Fact]
    public void Propriedade_De_Outro_Componente_Eh_Rejeitada()
    {
        // "address" pertence ao Contact, não ao Hero.
        var section = SiteSection.Create(SiteComponentType.Hero);

        Assert.Throws<DomainRuleException>(() =>
            section.SetProperty("address", "Rua X"));
    }

    [Fact]
    public void Remover_Propriedade_De_Configuracao()
    {
        var section = SiteSection.Create(SiteComponentType.Hero);
        section.SetProperty("subtitle", "Bem-vindo");

        section.RemoveProperty("subtitle");

        Assert.False(section.Configuration.ContainsKey("subtitle"));
    }

    [Fact]
    public void Secao_Incompleta_Quando_Falta_Obrigatoria()
    {
        var section = SiteSection.Create(SiteComponentType.Hero);

        Assert.False(section.IsComplete());

        section.SetProperty("title", "Barbearia XYZ");

        Assert.True(section.IsComplete());
    }

    [Fact]
    public void Secao_Com_Valor_Vazio_Nao_Esta_Completa()
    {
        var section = SiteSection.Create(SiteComponentType.Hero);
        section.SetProperty("title", "   ");

        Assert.False(section.IsComplete());
    }

    // ---------- F3.8: agendamento embutido ----------

    [Fact]
    public void BookingWidget_Aceita_Servico_E_Profissional()
    {
        var section = SiteSection.Create(SiteComponentType.BookingWidget);

        section.SetProperty("title", "Agende agora");
        section.SetProperty("serviceId", Guid.NewGuid().ToString());
        section.SetProperty("professionalId", Guid.NewGuid().ToString());

        Assert.True(section.IsComplete());
        Assert.Equal(3, section.Configuration.Count);
    }

    [Fact]
    public void BookingWidget_Existe_Na_Biblioteca()
    {
        Assert.True(SiteComponentSchema.IsSupported(SiteComponentType.BookingWidget));
    }

    // ---------- F3.5: previa ----------

    [Fact]
    public void Snapshot_Do_Site_Contem_Paginas_E_Tema()
    {
        var tenantId = Guid.NewGuid();
        var site = Site.Create(tenantId, "barbearia-xyz");
        var page = site.Pages.First();
        var hero = page.AddSection(SiteComponentType.Hero);
        hero.SetProperty("title", "Barbearia XYZ");

        var snapshot = site.BuildSnapshot();

        Assert.Contains("barbearia-xyz", snapshot);
        Assert.Contains("Hero", snapshot);
        Assert.Contains("Barbearia XYZ", snapshot);
        Assert.Contains("primary", snapshot);
    }

    [Fact]
    public void Snapshot_Reflete_Alteracao_Do_Rascunho()
    {
        var site = Site.Create(Guid.NewGuid(), "barbearia-xyz");
        var hero = site.Pages.First().AddSection(SiteComponentType.Hero);
        hero.SetProperty("title", "Antes");

        var before = site.BuildSnapshot();

        hero.SetProperty("title", "Depois");
        var after = site.BuildSnapshot();

        Assert.NotEqual(before, after);
        Assert.Contains("Depois", after);
    }
}
