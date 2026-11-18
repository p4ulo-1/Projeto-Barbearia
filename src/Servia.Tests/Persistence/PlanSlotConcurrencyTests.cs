using Microsoft.EntityFrameworkCore;
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
        var context = _database.CreateContext(ContextFor(TenantId));

        var planSlots = new PlanSlotService(
            new SubscriptionRepository(context),
            new PlanRepository(context),
            new ProfessionalCountReader(context));

        try
        {
            await planSlots.ExecuteWithSlotCheckAsync(
                TenantId,
                slotsConsumed: async token =>
                {
                    var professional = await context.Professionals
                        .FirstAsync(item => item.Id == professionalId, token);

                    var service = await context.Services
                        .FirstAsync(item => item.Id == serviceId, token);

                    var alreadyLinked = await context.ProfessionalServices
                        .AnyAsync(
                            link => link.ProfessionalId == professionalId
                                && link.ServiceId == serviceId,
                            token);

                    if (alreadyLinked)
                    {
                        return 0;
                    }

                    var occupiedBefore = await context.ProfessionalServices
                        .AnyAsync(
                            link => link.ProfessionalId == professionalId
                                && context.Services.Any(s =>
                                    s.Id == link.ServiceId && s.IsActive),
                            token);

                    var becomesEligible = !occupiedBefore
                        && BookingEligibility.OccupiesPlanSlot(professional, service.IsActive);

                    return becomesEligible ? 1 : 0;
                },
                operation: async token =>
                {
                    context.ProfessionalServices.Add(
                        ProfessionalService.Create(TenantId, professionalId, serviceId));

                    await context.SaveChangesAsync(token);

                    return true;
                });

            return true;
        }
        catch (Exception exception) when (IsSerializationConflict(exception))
        {
            return false;
        }
        catch (DomainRuleException)
        {
            return false;
        }
    }

    private static bool IsSerializationConflict(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            var message = current.Message;

            if (message.Contains("40001", StringComparison.Ordinal)
                || message.Contains("could not serialize", StringComparison.OrdinalIgnoreCase)
                || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<int> CountEligibleAsync()
    {
        await using var context = _database.CreateContext(ContextFor(TenantId));
        return await new ProfessionalCountReader(context).CountEligibleAsync(TenantId);
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

            subscription.ChangePlan(unlimited.Id);

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

        // Todas podem retornar sucesso (idempotência), mas o efeito é único.
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