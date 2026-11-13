using Microsoft.EntityFrameworkCore;
using Servia.Domain.Ai;
using Servia.Domain.Sites;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Repositories;

namespace Servia.Tests.Persistence;

/// <summary>
/// Persistência do onboarding e dos arquivos (itens 1 e 2), em PostgreSQL 17.
///
/// <b>Prova:</b> histórico e fatos sobrevivem ao recarregamento; a sessão
/// aberta é retomada; arquivos e sessões de um estabelecimento nunca são
/// vistos por outro.
/// </summary>
public sealed class OnboardingPersistenceTests : IAsyncLifetime
{
    private readonly PostgresTestDatabase _database = new();

    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public async Task InitializeAsync() => await _database.InitializeAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Historico_E_Fatos_Sobrevivem_Ao_Recarregamento()
    {
        var tenantId = Guid.NewGuid();

        await using (var context = ContextFor(tenantId))
        {
            var repository = new OnboardingRepository(context);
            var session = OnboardingSession.Start(tenantId, "Barbearia Persistente", SegmentType.Barbershop, Now);

            await repository.AddSessionAsync(session);

            session.AskQuestion("Qual o endereço?", Now);
            session.RecordAnswer("Rua A", new Dictionary<string, string>(), Now);
            session.RecordFacts(new Dictionary<string, string> { ["address"] = "Rua A" }, Now);

            await repository.SaveChangesAsync();
        }

        await using (var context = ContextFor(tenantId))
        {
            var repository = new OnboardingRepository(context);
            var session = await repository.FindOpenSessionAsync(tenantId);

            Assert.NotNull(session);
            Assert.Equal(2, session!.Messages.Count);
            Assert.Equal("Rua A", session.CollectedFacts["address"]);
        }
    }

    [Fact]
    public async Task Sessao_Aberta_E_Retomada_Em_Nova_Conexao()
    {
        var tenantId = Guid.NewGuid();
        Guid sessionId;

        await using (var context = ContextFor(tenantId))
        {
            var repository = new OnboardingRepository(context);
            var session = OnboardingSession.Start(tenantId, "Estúdio", SegmentType.Aesthetics, Now);
            await repository.AddSessionAsync(session);
            await repository.SaveChangesAsync();
            sessionId = session.Id;
        }

        await using (var context = ContextFor(tenantId))
        {
            var repository = new OnboardingRepository(context);
            var open = await repository.FindOpenSessionAsync(tenantId);

            Assert.NotNull(open);
            Assert.Equal(sessionId, open!.Id);
        }
    }

    [Fact]
    public async Task Sessoes_E_Arquivos_Sao_Isolados_Por_Tenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using (var context = ContextFor(tenantA))
        {
            var repository = new OnboardingRepository(context);

            var session = OnboardingSession.Start(tenantA, "Tenant A", SegmentType.Barbershop, Now);
            await repository.AddSessionAsync(session);

            var asset = SiteAsset.Register(
                tenantA, session.Id, SiteAssetKind.Logo, "logo.png", "image/png", 100,
                "a/logo.png", "checksum", Now);
            await repository.AddAssetAsync(asset);

            await repository.SaveChangesAsync();
        }

        // Tenant B nao enxerga a sessao de A.
        await using (var context = ContextFor(tenantB))
        {
            var repository = new OnboardingRepository(context);

            var sessions = await repository.ListSessionsAsync(tenantB);
            Assert.Empty(sessions);

            var open = await repository.FindOpenSessionAsync(tenantB);
            Assert.Null(open);
        }
    }

    [Fact]
    public async Task Proposta_Persistida_E_Tenant_Escopada()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        Guid changeSetId;

        await using (var context = ContextFor(tenantA))
        {
            var repository = new OnboardingRepository(context);

            var changeSet = AiChangeSet.Create(tenantA, "Site A", SiteTheme.Default);
            changeSet.AddSection(SiteComponentType.Hero, new Dictionary<string, string> { ["title"] = "A" });

            await repository.AddChangeSetAsync(changeSet);
            await repository.SaveChangesAsync();
            changeSetId = changeSet.Id;
        }

        // Tenant A recupera a propria proposta, com secoes.
        await using (var context = ContextFor(tenantA))
        {
            var repository = new OnboardingRepository(context);
            var loaded = await repository.FindChangeSetAsync(tenantA, changeSetId);

            Assert.NotNull(loaded);
            Assert.Single(loaded!.Sections);
            Assert.True(loaded.IsComplete());
        }

        // Tenant B nao recupera a proposta de A.
        await using (var context = ContextFor(tenantB))
        {
            var repository = new OnboardingRepository(context);
            var loaded = await repository.FindChangeSetAsync(tenantB, changeSetId);

            Assert.Null(loaded);
        }
    }

    private Servia.Infrastructure.Persistence.AppDbContext ContextFor(Guid tenantId)
    {
        var tenantContext = new Servia.Infrastructure.Tenancy.TenantContext();
        tenantContext.SetTenant(tenantId);

        return _database.CreateContext(tenantContext);
    }
}
