using Servia.Domain.Common;
using Servia.Domain.Sites;

namespace Servia.Tests.Sites;

/// <summary>
/// F3.1 — Site, SitePage, SiteSection, SiteTheme.
/// F3.3 — subdomínio.
/// </summary>
public class SiteTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static Site NewSite() => Site.Create(TenantId, "barbearia-xyz");

    // ---------- F3.1 / F3.3: criacao ----------

    [Fact]
    public void Criar_Site_Valido_Inicia_Como_Rascunho()
    {
        var site = NewSite();

        Assert.Equal("barbearia-xyz", site.Subdomain);
        Assert.Equal(SiteStatus.Draft, site.Status);
        Assert.False(site.IsPublished);
        Assert.Single(site.Pages); // página inicial
    }

    [Fact]
    public void Subdominio_Eh_Normalizado_Para_Minusculas()
    {
        var site = Site.Create(TenantId, "Barbearia-XYZ");

        Assert.Equal("barbearia-xyz", site.Subdomain);
    }

    [Theory]
    [InlineData("ab")]              // curto demais
    [InlineData("barbearia_xyz")]   // underscore
    [InlineData("-barbearia")]      // comeca com hifen
    [InlineData("barbearia-")]      // termina com hifen
    public void Criar_Site_Com_Subdominio_Invalido_Lanca_Excecao(string subdomain)
    {
        Assert.Throws<DomainRuleException>(() => Site.Create(TenantId, subdomain));
    }

    [Fact]
    public void Criar_Site_Sem_Tenant_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() => Site.Create(Guid.Empty, "barbearia-xyz"));
    }

    // ---------- Paginas ----------

    [Fact]
    public void Site_Nasce_Com_Pagina_Inicial()
    {
        var site = NewSite();

        Assert.Single(site.Pages);
        Assert.Equal(string.Empty, site.Pages.First().Slug);
        Assert.Equal("Início", site.Pages.First().Title);
    }

    [Fact]
    public void Adicionar_Pagina_Ao_Site()
    {
        var site = NewSite();

        var page = site.AddPage("servicos", "Serviços");

        Assert.Equal(2, site.Pages.Count);
        Assert.Equal("servicos", page.Slug);
    }

    [Fact]
    public void Adicionar_Pagina_Com_Slug_Duplicado_Lanca_Excecao()
    {
        var site = NewSite();
        site.AddPage("servicos", "Serviços");

        Assert.Throws<DomainRuleException>(() => site.AddPage("servicos", "Outra"));
    }

    [Fact]
    public void Pagina_Inicial_Nao_Pode_Ser_Removida()
    {
        var site = NewSite();
        var homeId = site.Pages.First().Id;

        Assert.Throws<DomainRuleException>(() => site.RemovePage(homeId));
    }

    [Fact]
    public void Remover_Pagina_Adicional()
    {
        var site = NewSite();
        var page = site.AddPage("servicos", "Serviços");

        site.RemovePage(page.Id);

        Assert.Single(site.Pages);
    }

    // ---------- Secoes ----------

    [Fact]
    public void Adicionar_Secao_A_Pagina()
    {
        var site = NewSite();
        var page = site.Pages.First();

        var section = page.AddSection(SiteComponentType.Hero);

        Assert.Single(page.Sections);
        Assert.Equal(SiteComponentType.Hero, section.ComponentType);
    }

    [Fact]
    public void Remover_Secao_Reordena_As_Demais()
    {
        var site = NewSite();
        var page = site.Pages.First();
        var hero = page.AddSection(SiteComponentType.Hero);
        page.AddSection(SiteComponentType.About);

        page.RemoveSection(hero.Id);

        Assert.Single(page.Sections);
        Assert.Equal(0, page.Sections.First().SortOrder);
    }

    [Fact]
    public void Mover_Secao_Altera_Ordem()
    {
        var site = NewSite();
        var page = site.Pages.First();
        var hero = page.AddSection(SiteComponentType.Hero);
        page.AddSection(SiteComponentType.About);

        page.MoveSection(hero.Id, 1);

        Assert.Equal(SiteComponentType.About, page.Sections.First().ComponentType);
        Assert.Equal(SiteComponentType.Hero, page.Sections.Last().ComponentType);
    }

    [Fact]
    public void Mover_Secao_Para_Posicao_Invalida_Lanca_Excecao()
    {
        var site = NewSite();
        var page = site.Pages.First();
        var hero = page.AddSection(SiteComponentType.Hero);

        Assert.Throws<DomainRuleException>(() => page.MoveSection(hero.Id, 5));
    }

    // ---------- Tema ----------

    [Fact]
    public void Site_Nasce_Com_Tema_Padrao()
    {
        var site = NewSite();

        Assert.Equal(SiteTheme.Default, site.Theme);
    }

    [Fact]
    public void Alterar_Tema_Do_Site()
    {
        var site = NewSite();
        var theme = SiteTheme.Create("#FF0000", "#00FF00", "Roboto", "modern");

        site.ChangeTheme(theme);

        Assert.Equal("#FF0000", site.Theme.PrimaryColor);
        Assert.Equal("modern", site.Theme.LayoutStyle);
    }
}
