using Microsoft.EntityFrameworkCore;
using Servia.Domain.Common;
using Servia.Domain.Sites;
using Servia.Infrastructure.Persistence;
using Servia.Infrastructure.Repositories;
using Servia.Infrastructure.Tenancy;

namespace Servia.Tests.Persistence;

/// <summary>
/// Concorrência na publicação do site (C10), em PostgreSQL 17.
///
/// <b>Prova:</b> duas publicações concorrentes do MESMO site não conseguem
/// gravar as duas — a segunda perde a disputa e recebe conflito. Sites de
/// estabelecimentos diferentes publicam simultaneamente sem interferência.
/// </summary>
public sealed class SiteConcurrencyPersistenceTests : IAsyncLifetime
{
    private readonly PostgresTestDatabase _database = new();

    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public async Task InitializeAsync() => await _database.InitializeAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private AppDbContext ContextFor(Guid tenantId)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId);
        return _database.CreateContext(tenantContext);
    }

    private static Site CreatePublishableSite(Guid tenantId, string subdomain)
    {
        var site = Site.Create(tenantId, subdomain);
        var page = site.Pages.First();
        var hero = page.AddSection(SiteComponentType.Hero);
        hero.SetProperty("title", "Bem-vindo");
        return site;
    }

    [Fact]
    public async Task Publicacoes_Concorrentes_Do_Mesmo_Site_Apenas_Uma_Vence()
    {
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        // Cria o site.
        await using (var seed = ContextFor(tenantId))
        {
            var site = CreatePublishableSite(tenantId, "concorrente-01");
            siteId = site.Id;
            seed.Sites.Add(site);
            await seed.SaveChangesAsync();
        }

        // Duas instancias carregam o MESMO site e tentam publicar.
        await using var contextA = ContextFor(tenantId);
        await using var contextB = ContextFor(tenantId);

        var siteA = await contextA.Sites.FirstAsync(site => site.Id == siteId);
        var siteB = await contextB.Sites.FirstAsync(site => site.Id == siteId);

        siteA.Publish(Guid.NewGuid(), Now);
        siteB.Publish(Guid.NewGuid(), Now);

        var repositoryA = new OnboardingRepository(contextA);
        var repositoryB = new OnboardingRepository(contextB);

        // A primeira grava.
        await repositoryA.SaveChangesAsync();

        // A segunda perde a disputa: o token de concorrencia nao confere.
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => repositoryB.SaveChangesAsync());

        // O banco tem exatamente uma publicacao.
        await using var verify = ContextFor(tenantId);
        var persisted = await verify.Sites.FirstAsync(site => site.Id == siteId);
        Assert.Single(persisted.Publications);
        Assert.Equal(1, persisted.Version);
    }

    [Fact]
    public async Task Estabelecimentos_Diferentes_Publicam_Simultaneamente()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        Guid siteAId;
        Guid siteBId;

        await using (var seed = ContextFor(tenantA))
        {
            var site = CreatePublishableSite(tenantA, "simultaneo-a");
            siteAId = site.Id;
            seed.Sites.Add(site);
            await seed.SaveChangesAsync();
        }

        await using (var seed = ContextFor(tenantB))
        {
            var site = CreatePublishableSite(tenantB, "simultaneo-b");
            siteBId = site.Id;
            seed.Sites.Add(site);
            await seed.SaveChangesAsync();
        }

        await using var contextA = ContextFor(tenantA);
        await using var contextB = ContextFor(tenantB);

        var siteA = await contextA.Sites.FirstAsync(site => site.Id == siteAId);
        var siteB = await contextB.Sites.FirstAsync(site => site.Id == siteBId);

        siteA.Publish(Guid.NewGuid(), Now);
        siteB.Publish(Guid.NewGuid(), Now);

        // Publicacoes de estabelecimentos diferentes nao disputam o mesmo token.
        await new OnboardingRepository(contextA).SaveChangesAsync();
        await new OnboardingRepository(contextB).SaveChangesAsync();

        await using var verifyA = ContextFor(tenantA);
        await using var verifyB = ContextFor(tenantB);

        Assert.Single((await verifyA.Sites.FirstAsync(site => site.Id == siteAId)).Publications);
        Assert.Single((await verifyB.Sites.FirstAsync(site => site.Id == siteBId)).Publications);
    }
}
