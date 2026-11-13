using Servia.Application.Sites;
using Servia.Domain.Ai;
using Servia.Domain.Common;
using Servia.Domain.Sites;

namespace Servia.Tests.Onboarding;

/// <summary>
/// Alterações direcionadas da IA em múltiplas páginas (C12).
///
/// Prova: alteração de uma página, de várias páginas, de elementos
/// específicos; rejeição de proposta; aplicação somente do aprovado;
/// preservação de páginas/elementos não envolvidos; validação de estrutura.
/// </summary>
public sealed class AiSiteChangeTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.NewGuid();

    private static Site BuildSite()
    {
        var site = Site.Create(TenantId, "barbearia-teste");

        var home = site.Pages.First();
        var hero = home.AddSection(SiteComponentType.Hero);
        hero.SetProperty("title", "Título antigo");
        hero.SetProperty("subtitle", "Subtítulo antigo");

        var about = site.AddPage("sobre", "Sobre");
        var aboutSection = about.AddSection(SiteComponentType.About);
        aboutSection.SetProperty("title", "Sobre antigo");
        aboutSection.SetProperty("body", "Corpo antigo");

        return site;
    }

    [Fact]
    public void Alteracao_De_Uma_Pagina_E_Aplicada()
    {
        var site = BuildSite();

        var change = AiSiteChange.Create(TenantId, "Troque o título do hero", Now);
        var page = change.AddPageChange(string.Empty);
        page.AddFieldChange(SiteComponentType.Hero, "title", "Título antigo", "Título novo");
        change.Approve(Now);

        var applied = SiteChangeApplier.Apply(site, change);

        Assert.Equal(1, applied);
        Assert.Equal("Título novo", site.Pages.First().Sections.First().Configuration["title"]);
    }

    [Fact]
    public void Alteracao_De_Multiplas_Paginas_E_Aplicada()
    {
        var site = BuildSite();

        var change = AiSiteChange.Create(TenantId, "Ajuste home e sobre", Now);

        var home = change.AddPageChange(string.Empty);
        home.AddFieldChange(SiteComponentType.Hero, "title", "Título antigo", "Home nova");

        var about = change.AddPageChange("sobre");
        about.AddFieldChange(SiteComponentType.About, "title", "Sobre antigo", "Sobre novo");

        change.Approve(Now);

        var applied = SiteChangeApplier.Apply(site, change);

        Assert.Equal(2, applied);
        Assert.Equal("Home nova", site.Pages.First().Sections.First().Configuration["title"]);
        Assert.Equal("Sobre novo",
            site.Pages.First(page => page.Slug == "sobre").Sections.First().Configuration["title"]);
    }

    [Fact]
    public void Elementos_Nao_Envolvidos_Sao_Preservados()
    {
        var site = BuildSite();

        var change = AiSiteChange.Create(TenantId, "Só o título", Now);
        var page = change.AddPageChange(string.Empty);
        page.AddFieldChange(SiteComponentType.Hero, "title", "Título antigo", "Título novo");
        change.Approve(Now);

        SiteChangeApplier.Apply(site, change);

        // O subtítulo do mesmo componente não foi tocado.
        Assert.Equal("Subtítulo antigo", site.Pages.First().Sections.First().Configuration["subtitle"]);

        // A página "sobre" permanece intacta.
        var about = site.Pages.First(item => item.Slug == "sobre");
        Assert.Equal("Sobre antigo", about.Sections.First().Configuration["title"]);
        Assert.Equal("Corpo antigo", about.Sections.First().Configuration["body"]);
    }

    [Fact]
    public void Proposta_Rejeitada_Nao_E_Aplicada()
    {
        var site = BuildSite();

        var change = AiSiteChange.Create(TenantId, "Troque o título", Now);
        var page = change.AddPageChange(string.Empty);
        page.AddFieldChange(SiteComponentType.Hero, "title", "Título antigo", "Título novo");
        change.Reject(Now);

        Assert.Throws<DomainRuleException>(() => SiteChangeApplier.Apply(site, change));
        Assert.Equal("Título antigo", site.Pages.First().Sections.First().Configuration["title"]);
    }

    [Fact]
    public void Proposta_Pendente_Nao_E_Aplicada()
    {
        var site = BuildSite();

        var change = AiSiteChange.Create(TenantId, "Troque o título", Now);
        var page = change.AddPageChange(string.Empty);
        page.AddFieldChange(SiteComponentType.Hero, "title", "Título antigo", "Título novo");

        // Ainda Proposed: não pode aplicar.
        Assert.Throws<DomainRuleException>(() => SiteChangeApplier.Apply(site, change));
    }

    [Fact]
    public void Proposta_Sem_Alteracoes_E_Invalida()
    {
        var change = AiSiteChange.Create(TenantId, "Nada a mudar", Now);

        Assert.Throws<DomainRuleException>(() => change.Approve(Now));
    }

    [Fact]
    public void Propriedade_Fora_Do_Schema_E_Rejeitada()
    {
        var change = AiSiteChange.Create(TenantId, "Campo inválido", Now);
        var page = change.AddPageChange(string.Empty);
        page.AddFieldChange(SiteComponentType.Hero, "codigoArbitrario", null, "<script>");

        Assert.Throws<DomainRuleException>(() => change.Approve(Now));
    }

    [Fact]
    public void Solicitacao_E_Preservada_Para_Rastreabilidade()
    {
        var change = AiSiteChange.Create(TenantId, "  Troque o título do hero  ", Now);

        Assert.Equal("Troque o título do hero", change.Request);
        Assert.Equal(AiSiteChangeStatus.Proposed, change.Status);
    }

    [Fact]
    public void Pagina_Inexistente_Nao_E_Criada()
    {
        var site = BuildSite();

        var change = AiSiteChange.Create(TenantId, "Página que não existe", Now);
        var page = change.AddPageChange("inexistente");
        page.AddFieldChange(SiteComponentType.Hero, "title", null, "Novo");
        change.Approve(Now);

        var applied = SiteChangeApplier.Apply(site, change);

        Assert.Equal(0, applied);
        Assert.DoesNotContain(site.Pages, item => item.Slug == "inexistente");
    }

    [Fact]
    public void Alteracao_E_Tenant_Escopada()
    {
        var change = AiSiteChange.Create(TenantId, "Alteração", Now);

        Assert.Equal(TenantId, change.TenantId);
        Assert.Throws<DomainRuleException>(() => AiSiteChange.Create(Guid.Empty, "x", Now));
    }
}
