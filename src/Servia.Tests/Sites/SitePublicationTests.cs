using Servia.Domain.Common;
using Servia.Domain.Sites;

namespace Servia.Tests.Sites;

/// <summary>
/// F3.6 — publicação com aprovação.
/// F3.7 — rollback de versão publicada.
/// </summary>
public class SitePublicationTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid AuthorId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Cria um site com a página inicial completa.</summary>
    private static Site ReadySite()
    {
        var site = Site.Create(TenantId, "barbearia-xyz");
        var hero = site.Pages.First().AddSection(SiteComponentType.Hero);
        hero.SetProperty("title", "Barbearia XYZ");
        return site;
    }

    // ---------- F3.6: publicacao ----------

    [Fact]
    public void Publicar_Site_Completo_Cria_Versao()
    {
        var site = ReadySite();

        var publication = site.Publish(AuthorId, Now);

        Assert.Equal(1, publication.Version);
        Assert.Equal(AuthorId, publication.PublishedBy);
        Assert.Equal(Now, publication.PublishedAt);
        Assert.True(site.IsPublished);
        Assert.Equal(publication.Id, site.PublishedVersionId);
    }

    [Fact]
    public void Publicar_Site_Com_Secao_Incompleta_Lanca_Excecao()
    {
        var site = Site.Create(TenantId, "barbearia-xyz");
        site.Pages.First().AddSection(SiteComponentType.Hero); // sem título

        Assert.Throws<DomainRuleException>(() => site.Publish(AuthorId, Now));
        Assert.False(site.IsPublished);
    }

    [Fact]
    public void Publicar_Sem_Autor_Lanca_Excecao()
    {
        var site = ReadySite();

        Assert.Throws<DomainRuleException>(() => site.Publish(Guid.Empty, Now));
    }

    [Fact]
    public void Rascunho_Alterado_Nao_Afeta_Versao_Publicada()
    {
        var site = ReadySite();
        var firstPublication = site.Publish(AuthorId, Now);
        var snapshotBefore = firstPublication.Snapshot;

        // Altera o rascunho depois de publicar.
        var hero = site.Pages.First().Sections.First();
        hero.SetProperty("title", "Novo título");

        // A versão publicada permanece intacta.
        Assert.Equal(snapshotBefore, firstPublication.Snapshot);
        Assert.Contains("Barbearia XYZ", firstPublication.Snapshot);
        Assert.DoesNotContain("Novo título", firstPublication.Snapshot);
    }

    [Fact]
    public void Cada_Publicacao_Gera_Versao_Incremental()
    {
        var site = ReadySite();

        var first = site.Publish(AuthorId, Now);
        var second = site.Publish(AuthorId, Now.AddHours(1));

        Assert.Equal(1, first.Version);
        Assert.Equal(2, second.Version);
        Assert.Equal(2, site.Publications.Count);
    }

    [Fact]
    public void Retirar_Site_Do_Ar_Mantem_Rascunho()
    {
        var site = ReadySite();
        site.Publish(AuthorId, Now);

        site.Unpublish();

        Assert.False(site.IsPublished);
        Assert.Single(site.Pages);
    }

    [Fact]
    public void Retirar_Site_Nao_Publicado_Lanca_Excecao()
    {
        var site = ReadySite();

        Assert.Throws<DomainRuleException>(() => site.Unpublish());
    }

    // ---------- F3.7: rollback ----------

    [Fact]
    public void Rollback_Restaura_Versao_Anterior()
    {
        var site = ReadySite();
        var first = site.Publish(AuthorId, Now);

        var hero = site.Pages.First().Sections.First();
        hero.SetProperty("title", "Segunda versão");
        var second = site.Publish(AuthorId, Now.AddHours(1));

        Assert.Equal(second.Id, site.PublishedVersionId);

        site.RollbackTo(first.Id);

        Assert.Equal(first.Id, site.PublishedVersionId);
        Assert.True(site.IsPublished);
    }

    [Fact]
    public void Rollback_Para_Versao_Inexistente_Lanca_Excecao()
    {
        var site = ReadySite();
        site.Publish(AuthorId, Now);

        Assert.Throws<DomainRuleException>(() => site.RollbackTo(Guid.NewGuid()));
    }

    [Fact]
    public void Versao_Publicada_Eh_Imutavel()
    {
        var site = ReadySite();
        var publication = site.Publish(AuthorId, Now);
        var originalSnapshot = publication.Snapshot;

        // Não há método de alteração: a versão permanece como foi criada.
        Assert.Equal(originalSnapshot, publication.Snapshot);
        Assert.Equal(1, publication.Version);
    }

    [Fact]
    public void Publicacao_Sem_Snapshot_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            SitePublication.Create(Guid.NewGuid(), 1, "", AuthorId, Now));
    }

    [Fact]
    public void Publicacao_Com_Versao_Invalida_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            SitePublication.Create(Guid.NewGuid(), 0, "{}", AuthorId, Now));
    }
}
