using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Servia.Api.Controllers;
using Servia.Application.Billing;
using Servia.Domain.Appointments;
using Servia.Domain.Catalog;
using Servia.Domain.Common;
using Servia.Domain.Team;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;
using Servia.Infrastructure.Repositories;
using Servia.Infrastructure.Tenancy;

namespace Servia.Tests.Persistence;

/// <summary>
/// Atomicidade da ocupação de vagas do plano, contra PostgreSQL 17 real.
///
/// <b>O defeito que estes testes provam corrigido:</b> verificar o limite
/// fora da transação permite que duas requisições concorrentes contem a mesma
/// vaga livre e ambas gravem. O limite é ultrapassado sem que nenhuma regra
/// tenha sido violada isoladamente.
///
/// <b>Como a correção funciona:</b> contagem, validação e gravação rodam na
/// mesma transação serializável. O PostgreSQL aborta uma das transações
/// concorrentes com erro de serialização.
///
/// Testes sequenciais não provam atomicidade: por isso cada cenário dispara
/// requisições simultâneas, cada uma com seu próprio contexto, como em
/// produção.
/// </summary>
public class PlanSlotConcurrencyTests : IAsyncLifetime
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 8, 0, 0, TimeSpan.Zero);

    private readonly PostgresTestDatabase _database = new();

    private Guid _planId;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();

        await using var context = _database.CreateContext(ContextFor(TenantId));

        // Plano com uma única vaga: o limite fica observável com dois
        // profissionais.
        var plan = Plan.Create(
            $"VAGA1-{TenantId.ToString("N")[..12]}",
            "Uma vaga",
            Money.From(99m),
            includedProfessionals: 1,
            SiteCustomizationLevel.Basic,
            includedAiUnits: 0);

        context.Plans.Add(plan);

        var today = DateOnly.FromDateTime(Now.UtcDateTime);

        context.Subscriptions.Add(Subscription.StartTrial(TenantId, plan.Id, today));

        await context.SaveChangesAsync();

        _planId = plan.Id;
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private static ITenantContext ContextFor(Guid tenantId)
    {
        var context = new TenantContext();
        context.SetTenant(tenantId);
        return context;
    }

    /// <summary>
    /// Cria um profissional ativo sem serviço — portanto não elegível — e um
    /// serviço ativo separado para ele.
    /// </summary>
    private async Task<(Guid ProfessionalId, Guid ServiceId)> SeedIneligibleAsync(string name)
    {
        await using var context = _database.CreateContext(ContextFor(TenantId));

        var professional = Professional.Create(
            TenantId,
            name,
            WorkSchedule.Create(
                [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday],
                new TimeOnly(9, 0),
                new TimeOnly(18, 0)));

        var service = Service.Create(TenantId, $"Serviço de {name}", Money.From(50m), 30);

        context.Professionals.Add(professional);
        context.Services.Add(service);
        await context.SaveChangesAsync();

        return (professional.Id, service.Id);
    }

    /// <summary>
    /// Executa a associação como a API faz: cada chamada tem seu próprio
    /// contexto, repositório e serviço de vagas.
    /// </summary>
    private async Task<bool> TryLinkAsync(Guid professionalId, Guid serviceId)
    {
        // Cada repetição usa contexto novo, como uma nova requisição do cliente.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var tenant = ContextFor(TenantId);
            await using var context = _database.CreateContext(tenant);
            var subscriptions = new SubscriptionRepository(context);
            var planSlots = new PlanSlotService(
                subscriptions, new PlanRepository(context),
                new ProfessionalCountReader(context));
            var controller = new TeamController(context, tenant, planSlots, subscriptions);
            try
            {
                var response = await controller.LinkServiceAsync(professionalId, serviceId, default);
                return response is Microsoft.AspNetCore.Mvc.NoContentResult;
            }
            catch (ConcurrencyConflictException) when (attempt < 2) { }
            catch (ConcurrencyConflictException) { return false; }
            catch (DomainRuleException) { return false; }
        }
        return false;
    }

    private async Task<int> CountEligibleAsync()
    {
        await using var context = _database.CreateContext(ContextFor(TenantId));
        return await new ProfessionalCountReader(context).CountEligibleAsync(TenantId);
    }

    [Theory]
    [InlineData("professional")]
    [InlineData("service")]
    [InlineData("schedule")]
    public async Task Transicao_Que_Excede_Limite_Faz_Rollback_E_Limpa_Contexto(string transition)
    {
        var occupying = await SeedIneligibleAsync("Ocupante");
        Assert.True(await TryLinkAsync(occupying.ProfessionalId, occupying.ServiceId));
        var candidate = await SeedIneligibleAsync("Candidato");
        await using var context = _database.CreateContext(ContextFor(TenantId));
        var professional = await context.Professionals.SingleAsync(p => p.Id == candidate.ProfessionalId);
        var service = await context.Services.SingleAsync(s => s.Id == candidate.ServiceId);
        if (transition == "professional") professional.Deactivate();
        if (transition == "service") service.Deactivate();
        context.ProfessionalServices.Add(ProfessionalService.Create(TenantId, professional.Id, service.Id));
        await context.SaveChangesAsync();
        if (transition == "schedule")
        {
            // Dado materializado sem jornada: deve ser rejeitado pelo predicado.
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"Professionals\" SET \"Schedule_WorkDays\" = '' WHERE \"Id\" = {professional.Id}");
        }
        context.ChangeTracker.Clear();
        Assert.Equal(1, await new ProfessionalCountReader(context).CountEligibleAsync(TenantId));
        var slots = new PlanSlotService(new SubscriptionRepository(context),
            new PlanRepository(context), new ProfessionalCountReader(context));
        await Assert.ThrowsAsync<DomainRuleException>(() => slots.ExecuteWithSlotCheckAsync(TenantId, async token =>
        {
            var person = await context.Professionals.SingleAsync(p => p.Id == candidate.ProfessionalId, token);
            var item = await context.Services.SingleAsync(s => s.Id == candidate.ServiceId, token);
            if (transition == "professional") person.Activate();
            if (transition == "service") item.Activate();
            if (transition == "schedule") person.UpdateSchedule(WorkSchedule.Create(
                [DayOfWeek.Monday], new TimeOnly(9, 0), new TimeOnly(18, 0)));
            await context.SaveChangesAsync(token);
            return true;
        }));
        Assert.Empty(context.ChangeTracker.Entries());
        Assert.Equal(1, await CountEligibleAsync());
        await using var verification = _database.CreateContext(ContextFor(TenantId));
        var savedPerson = await verification.Professionals.SingleAsync(p => p.Id == candidate.ProfessionalId);
        var savedService = await verification.Services.SingleAsync(s => s.Id == candidate.ServiceId);
        if (transition == "professional") Assert.False(savedPerson.IsActive);
        if (transition == "service") Assert.False(savedService.IsActive);
        if (transition == "schedule") Assert.Empty(savedPerson.Schedule.WorkDays);
    }

    // ---------- Concorrência real ----------

    [Fact]
    public async Task Associacoes_Simultaneas_Disputando_A_Unica_Vaga_Aceitam_Apenas_Uma()
    {
        // Dois profissionais não elegíveis, cada um com seu serviço ativo.
        var first = await SeedIneligibleAsync("Profissional A");
        var second = await SeedIneligibleAsync("Profissional B");

        Assert.Equal(0, await CountEligibleAsync());

        // Ambos tentam se tornar elegíveis ao mesmo tempo.
        var results = await Task.WhenAll(
            TryLinkAsync(first.ProfessionalId, first.ServiceId),
            TryLinkAsync(second.ProfessionalId, second.ServiceId));

        var accepted = results.Count(result => result);

        // Exatamente uma associação vence: a vaga é única.
        Assert.Equal(1, accepted);

        // A contagem persistida respeita o limite contratado.
        Assert.Equal(1, await CountEligibleAsync());

        // E apenas um vínculo foi gravado.
        await using var context = _database.CreateContext(ContextFor(TenantId));
        Assert.Equal(1, await context.ProfessionalServices.CountAsync());
    }

    [Fact]
    public async Task Associacao_Concorrente_Com_Correcao_De_Preco_Grava_Apenas_Uma_Ocupacao()
    {
        var legacy = await SeedIneligibleAsync("Serviço com preço inválido");
        var newcomer = await SeedIneligibleAsync("Novo ocupante");
        var tenant = ContextFor(TenantId);
        await using (var setup = _database.CreateContext(tenant))
        {
            await setup.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"Services\" SET \"Price_Amount\" = -1 WHERE \"Id\" = {legacy.ServiceId}");
        }
        Assert.True(await TryLinkAsync(legacy.ProfessionalId, legacy.ServiceId));

        await using var context = _database.CreateContext(tenant);
        var counts = new PausedCountReader(new ProfessionalCountReader(context));
        var subscriptions = new SubscriptionRepository(context);
        var catalog = new CatalogController(context, tenant,
            new PlanSlotService(subscriptions, new PlanRepository(context), counts), subscriptions);
        var request = Record.ExceptionAsync(() =>
            catalog.ChangeServicePriceAsync(legacy.ServiceId, new ChangePriceRequest(50m), default));
        try
        {
            Assert.Equal(0, await counts.WaitForFirstCountAsync());
            Assert.True(await TryLinkAsync(newcomer.ProfessionalId, newcomer.ServiceId));
        }
        finally
        {
            counts.Resume();
        }

        Assert.IsType<ConcurrencyConflictException>(await request);
        await Assert.ThrowsAsync<DomainRuleException>(() =>
            catalog.ChangeServicePriceAsync(legacy.ServiceId, new ChangePriceRequest(50m), default));
        await using var verification = _database.CreateContext(tenant);
        var storedService = await verification.Services.SingleAsync(item => item.Id == legacy.ServiceId);
        Assert.Equal(-1m, storedService.Price.Amount);
        Assert.Equal(2, await verification.ProfessionalServices.CountAsync());
        Assert.Equal(1, await CountEligibleAsync());
    }

    [Fact]
    public async Task Desativacao_Concorrente_Libera_Vaga_Para_Nova_Tentativa_De_Associacao()
    {
        var occupying = await SeedIneligibleAsync("Ocupante");
        var newcomer = await SeedIneligibleAsync("Novo ocupante");
        Assert.True(await TryLinkAsync(occupying.ProfessionalId, occupying.ServiceId));
        var tenant = ContextFor(TenantId);

        await using var linkingContext = _database.CreateContext(tenant);
        var counts = new PausedCountReader(new ProfessionalCountReader(linkingContext));
        var linkingSubscriptions = new SubscriptionRepository(linkingContext);
        var team = new TeamController(linkingContext, tenant, new PlanSlotService(
            linkingSubscriptions, new PlanRepository(linkingContext), counts), linkingSubscriptions);

        await using var catalogContext = _database.CreateContext(tenant);
        var subscriptions = new SubscriptionRepository(catalogContext);
        var catalog = new CatalogController(catalogContext, tenant,
            new PlanSlotService(subscriptions, new PlanRepository(catalogContext),
                new ProfessionalCountReader(catalogContext)), subscriptions);
        var request = Record.ExceptionAsync(() =>
            team.LinkServiceAsync(newcomer.ProfessionalId, newcomer.ServiceId, default));
        try
        {
            Assert.Equal(1, await counts.WaitForFirstCountAsync());
            Assert.IsType<NoContentResult>(await catalog.DeactivateServiceAsync(occupying.ServiceId, default));
        }
        finally
        {
            counts.Resume();
        }

        Assert.IsType<DomainRuleException>(await request);
        Assert.Equal(0, await CountEligibleAsync());
        await using var verification = _database.CreateContext(tenant);
        Assert.False(await verification.ProfessionalServices.AnyAsync(link => link.ProfessionalId == newcomer.ProfessionalId));
        Assert.IsType<NoContentResult>(await catalog.DeactivateServiceAsync(occupying.ServiceId, default));
        Assert.True(await TryLinkAsync(newcomer.ProfessionalId, newcomer.ServiceId));
        Assert.Equal(1, await CountEligibleAsync());
    }

    private sealed class PausedCountReader(IProfessionalCountReader reader) : IProfessionalCountReader
    {
        private readonly TaskCompletionSource<int> _firstCount = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _continue = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<int> WaitForFirstCountAsync() => _firstCount.Task.WaitAsync(TimeSpan.FromSeconds(15));

        public void Resume() => _continue.TrySetResult();

        public async Task<int> CountEligibleAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            var count = await reader.CountEligibleAsync(tenantId, cancellationToken);
            if (_firstCount.TrySetResult(count))
            {
                await _continue.Task.WaitAsync(cancellationToken);
            }
            return count;
        }
    }

    [Fact]
    public async Task Associacoes_Simultaneas_Com_Vagas_Suficientes_Sao_Todas_Aceitas()
    {
        // Plano ilimitado: sem disputa, todas as associações devem passar.
        await using (var context = _database.CreateContext(ContextFor(TenantId)))
        {
            var unlimited = Plan.Create(
                $"ILIM-{TenantId.ToString("N")[..12]}",
                "Ilimitado",
                Money.From(499m),
                includedProfessionals: 0,
                SiteCustomizationLevel.Full,
                includedAiUnits: 0);

            context.Plans.Add(unlimited);

            var subscription = await context.Subscriptions
                .IgnoreQueryFilters()
                .FirstAsync(item => item.TenantId == TenantId);

            context.Entry(subscription).Property(item => item.PlanId).CurrentValue = unlimited.Id;

            await context.SaveChangesAsync();
        }

        var first = await SeedIneligibleAsync("Profissional C");
        var second = await SeedIneligibleAsync("Profissional D");

        var results = await Task.WhenAll(
            TryLinkAsync(first.ProfessionalId, first.ServiceId),
            TryLinkAsync(second.ProfessionalId, second.ServiceId));

        Assert.All(results, Assert.True);
        Assert.Equal(2, await CountEligibleAsync());
    }

    [Fact]
    public async Task Associacao_Repetida_Simultanea_Nao_Duplica_Nem_Consome_Duas_Vagas()
    {
        var professional = await SeedIneligibleAsync("Profissional E");

        // A mesma associação, disparada várias vezes ao mesmo tempo.
        var results = await Task.WhenAll(
            Enumerable.Range(0, 4)
                .Select(_ => TryLinkAsync(professional.ProfessionalId, professional.ServiceId)));

        Assert.All(results, Assert.True);

        // Repetições controladas preservam o efeito único.
        await using var context = _database.CreateContext(ContextFor(TenantId));

        Assert.Equal(1, await context.ProfessionalServices.CountAsync());
        Assert.Equal(1, await CountEligibleAsync());
    }

    [Fact]
    public async Task Associacao_Nao_Consome_Vaga_Quando_O_Profissional_Ja_Era_Elegivel()
    {
        // Ocupa a única vaga com o primeiro serviço.
        var professional = await SeedIneligibleAsync("Profissional F");

        Assert.True(await TryLinkAsync(professional.ProfessionalId, professional.ServiceId));
        Assert.Equal(1, await CountEligibleAsync());

        // Um segundo serviço para quem já ocupa vaga não consome outra.
        await using (var context = _database.CreateContext(ContextFor(TenantId)))
        {
            var extra = Service.Create(TenantId, "Serviço extra", Money.From(30m), 30);
            context.Services.Add(extra);
            await context.SaveChangesAsync();

            Assert.True(await TryLinkAsync(professional.ProfessionalId, extra.Id));
        }

        Assert.Equal(1, await CountEligibleAsync());
    }

    [Fact]
    public async Task Isolamento_Entre_Estabelecimentos_Na_Contagem()
    {
        // Outro tenant, com o próprio plano e profissional elegível.
        var otherTenant = Guid.NewGuid();

        await using (var context = _database.CreateContext(ContextFor(otherTenant)))
        {
            var plan = Plan.Create(
                $"OUTRO-{otherTenant.ToString("N")[..12]}",
                "Outro plano",
                Money.From(99m),
                includedProfessionals: 1,
                SiteCustomizationLevel.Basic,
                includedAiUnits: 0);

            context.Plans.Add(plan);

            var professional = Professional.Create(
                otherTenant,
                "Profissional de outro tenant",
                WorkSchedule.Create([DayOfWeek.Monday], new TimeOnly(9, 0), new TimeOnly(18, 0)));

            var service = Service.Create(otherTenant, "Serviço do outro", Money.From(50m), 30);

            context.Professionals.Add(professional);
            context.Services.Add(service);
            context.ProfessionalServices.Add(
                ProfessionalService.Create(otherTenant, professional.Id, service.Id));

            await context.SaveChangesAsync();
        }

        // A contagem do tenant original não enxerga o profissional do outro.
        Assert.Equal(0, await CountEligibleAsync());

        await using var otherContext = _database.CreateContext(ContextFor(otherTenant));
        Assert.Equal(1, await new ProfessionalCountReader(otherContext).CountEligibleAsync(otherTenant));
    }
}
