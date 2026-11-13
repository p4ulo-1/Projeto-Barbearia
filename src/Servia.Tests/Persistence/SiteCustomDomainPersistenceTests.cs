using Microsoft.EntityFrameworkCore;
using Servia.Domain.Sites;
using Servia.Infrastructure.Persistence;
using Servia.Infrastructure.Tenancy;

namespace Servia.Tests.Persistence;

/// <summary>
/// Unicidade de <see cref="Site.CustomDomain"/> no PostgreSQL (melhoria da auditoria).
///
/// Prova que o índice único parcial aceita vários NULL, rejeita domínio
/// duplicado e rejeita duplicidade que só difere por caixa (porque o valor é
/// gravado em minúsculas).
/// </summary>
public sealed class SiteCustomDomainPersistenceTests : IAsyncLifetime
{
    private readonly PostgresTestDatabase _database = new();

    public async Task InitializeAsync() => await _database.InitializeAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private AppDbContext ContextFor(Guid tenantId)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(tenantId);
        return _database.CreateContext(tenantContext);
    }

    private static Site NewSite(Guid tenantId, string suffix) =>
        Site.Create(tenantId, "site-" + suffix);

    [Fact]
    public async Task Varios_Sites_Sem_Dominio_Proprio_Sao_Permitidos()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using (var a = ContextFor(tenantA))
        {
            a.Sites.Add(NewSite(tenantA, "a"));
            await a.SaveChangesAsync();
        }

        await using (var b = ContextFor(tenantB))
        {
            b.Sites.Add(NewSite(tenantB, "b"));
            await b.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Dominio_Duplicado_E_Rejeitado()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        const string domain = "barbearia.exemplo.com";

        await using (var a = ContextFor(tenantA))
        {
            var site = NewSite(tenantA, "a");
            site.SetCustomDomain(domain);
            a.Sites.Add(site);
            await a.SaveChangesAsync();
        }

        await using (var b = ContextFor(tenantB))
        {
            var site = NewSite(tenantB, "b");
            site.SetCustomDomain(domain);
            b.Sites.Add(site);

            await Assert.ThrowsAsync<DbUpdateException>(() => b.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task Dominio_Que_Difere_Apenas_Por_Caixa_Tambem_E_Rejeitado()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using (var a = ContextFor(tenantA))
        {
            var site = NewSite(tenantA, "a");
            site.SetCustomDomain("barbearia.exemplo.com");
            a.Sites.Add(site);
            await a.SaveChangesAsync();
        }

        await using (var b = ContextFor(tenantB))
        {
            var site = NewSite(tenantB, "b");
            // SetCustomDomain normaliza para minusculas; mesmo assim o indice
            // rejeita o duplicado.
            site.SetCustomDomain("BARBEARIA.EXEMPLO.COM");
            b.Sites.Add(site);

            await Assert.ThrowsAsync<DbUpdateException>(() => b.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task Dominio_Persistido_Em_Minusculas()
    {
        var tenantId = Guid.NewGuid();

        await using (var context = ContextFor(tenantId))
        {
            var site = NewSite(tenantId, "a");
            site.SetCustomDomain("MinhaLoja.Exemplo.COM");
            context.Sites.Add(site);
            await context.SaveChangesAsync();
        }

        await using (var verify = ContextFor(tenantId))
        {
            var loaded = await verify.Sites.FirstAsync();
            Assert.Equal("minhaloja.exemplo.com", loaded.CustomDomain);
        }
    }
}
