using Microsoft.EntityFrameworkCore;
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
/// Contagem de profissionais que ocupam vaga no plano.
///
/// <b>Critério aprovado (ESCOPO_AV2 §2.3):</b> profissional ativo, com ao
/// menos um serviço ativo associado e com jornada configurada. Não conta
/// cargo, usuário vinculado nem cadastro isolado.
///
/// Contra PostgreSQL 17, para provar que a consulta de contagem respeita o
/// vínculo persistido com o serviço.
/// </summary>
public class ProfessionalCountByRoleTests : IAsyncLifetime
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private readonly PostgresTestDatabase _database = new();

    public async Task InitializeAsync() => await _database.InitializeAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private static ITenantContext ContextFor(Guid tenantId)
    {
        var context = new TenantContext();
        context.SetTenant(tenantId);
        return context;
    }

    private static WorkSchedule Schedule() =>
        WorkSchedule.Create(
            [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday],
            new TimeOnly(9, 0),
            new TimeOnly(18, 0));

    /// <summary>
    /// Cria um profissional e, quando pedido, um serviço ativo vinculado a
    /// ele. O vínculo é o que torna a pessoa elegível para ocupar vaga.
    /// </summary>
    private async Task<Guid> AddProfessionalAsync(
        string name,
        bool withActiveService,
        bool isActive = true)
    {
        await using var context = _database.CreateContext(ContextFor(TenantId));

        var professional = Professional.Create(TenantId, name, Schedule());

        if (!isActive)
        {
            professional.Deactivate();
        }

        context.Professionals.Add(professional);

        if (withActiveService)
        {
            var service = Service.Create(TenantId, $"Serviço de {name}", Money.From(50m), 30);

            context.Services.Add(service);
            context.ProfessionalServices.Add(
                ProfessionalService.Create(TenantId, professional.Id, service.Id));
        }

        await context.SaveChangesAsync();

        return professional.Id;
    }

    private async Task<int> CountAsync()
    {
        await using var context = _database.CreateContext(ContextFor(TenantId));
        return await new ProfessionalCountReader(context).CountEligibleAsync(TenantId);
    }

    [Fact]
    public async Task Conta_Apenas_Quem_Tem_Servico_Ativo_Associado()
    {
        // Com serviço ativo: ocupa vaga.
        await AddProfessionalAsync("Com serviço", withActiveService: true);

        // Sem serviço: cadastro isolado não consome vaga.
        await AddProfessionalAsync("Sem serviço", withActiveService: false);

        Assert.Equal(1, await CountAsync());
    }

    [Fact]
    public async Task Servico_Inativo_Nao_Torna_O_Profissional_Elegivel()
    {
        var professionalId = await AddProfessionalAsync("Com serviço", withActiveService: true);

        Assert.Equal(1, await CountAsync());

        // Desativar o serviço remove a elegibilidade: não há serviço agendável.
        await using (var context = _database.CreateContext(ContextFor(TenantId)))
        {
            var service = await context.Services.SingleAsync();
            service.Deactivate();
            await context.SaveChangesAsync();
        }

        Assert.Equal(0, await CountAsync());
    }

    [Fact]
    public async Task Profissional_Inativo_Nao_Conta()
    {
        await AddProfessionalAsync("Ativo", withActiveService: true);
        await AddProfessionalAsync("Inativo", withActiveService: true, isActive: false);

        Assert.Equal(1, await CountAsync());
    }

    [Fact]
    public async Task Vinculo_Torna_O_Profissional_Elegivel()
    {
        // Cadastro sem serviço: não ocupa vaga.
        var professionalId = await AddProfessionalAsync("Pessoa", withActiveService: false);

        Assert.Equal(0, await CountAsync());

        // Ao receber um serviço ativo, passa a ocupar vaga.
        await using (var context = _database.CreateContext(ContextFor(TenantId)))
        {
            var service = Service.Create(TenantId, "Serviço novo", Money.From(40m), 30);

            context.Services.Add(service);
            context.ProfessionalServices.Add(
                ProfessionalService.Create(TenantId, professionalId, service.Id));

            await context.SaveChangesAsync();
        }

        Assert.Equal(1, await CountAsync());
    }

    [Fact]
    public async Task Cargo_Nao_Participa_Da_Contagem()
    {
        // IsServiceProvider é um atributo de atuação, não o critério de vaga.
        // Quem tem serviço ativo conta, independentemente do atributo.
        await using (var context = _database.CreateContext(ContextFor(TenantId)))
        {
            var professional = Professional.Create(
                TenantId, "Sem atributo de prestador", Schedule(), isServiceProvider: false);

            var service = Service.Create(TenantId, "Serviço", Money.From(50m), 30);

            context.Professionals.Add(professional);
            context.Services.Add(service);
            context.ProfessionalServices.Add(
                ProfessionalService.Create(TenantId, professional.Id, service.Id));

            await context.SaveChangesAsync();
        }

        Assert.Equal(1, await CountAsync());
    }
}