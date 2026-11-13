using Microsoft.EntityFrameworkCore;
using Servia.Domain.Appointments;
using Servia.Domain.Team;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;
using Servia.Infrastructure.Repositories;
using Servia.Infrastructure.Tenancy;

namespace Servia.Tests.Persistence;

/// <summary>
/// Seção 2 do pedido de ajustes: a contagem de profissionais do plano
/// considera exclusivamente quem presta serviços, independentemente do
/// perfil de acesso.
///
/// Contra PostgreSQL 17, para provar que a coluna nova é persistida e que a
/// consulta de contagem a respeita.
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

    private async Task AddProfessionalAsync(string name, bool isServiceProvider)
    {
        await using var context = _database.CreateContext(ContextFor(TenantId));

        context.Professionals.Add(
            Professional.Create(TenantId, name, Schedule(), isServiceProvider: isServiceProvider));

        await context.SaveChangesAsync();
    }

    private async Task<int> CountAsync()
    {
        await using var context = _database.CreateContext(ContextFor(TenantId));
        return await new ProfessionalCountReader(context).CountActiveAsync(TenantId);
    }

    [Fact]
    public async Task Conta_Apenas_Prestadores_De_Servico()
    {
        // Administrador que atende (prestador) e administrador que não atende.
        await AddProfessionalAsync("Admin que atende", isServiceProvider: true);
        await AddProfessionalAsync("Admin que não atende", isServiceProvider: false);

        // Recepcionista que atende e recepcionista que não atende.
        await AddProfessionalAsync("Recepção que atende", isServiceProvider: true);
        await AddProfessionalAsync("Recepção que não atende", isServiceProvider: false);

        // Profissional que atende.
        await AddProfessionalAsync("Profissional", isServiceProvider: true);

        Assert.Equal(3, await CountAsync());
    }

    [Fact]
    public async Task Atuacao_Persistida_E_Atualiza_A_Contagem()
    {
        await AddProfessionalAsync("Pessoa", isServiceProvider: false);

        Assert.Equal(0, await CountAsync());

        // Passa a prestar serviços: passa a ocupar vaga.
        await using (var context = _database.CreateContext(ContextFor(TenantId)))
        {
            var professional = await context.Professionals.SingleAsync();
            professional.SetServiceProvider(true);
            await context.SaveChangesAsync();
        }

        Assert.Equal(1, await CountAsync());

        // Deixa de prestar: libera a vaga.
        await using (var context = _database.CreateContext(ContextFor(TenantId)))
        {
            var professional = await context.Professionals.SingleAsync();
            professional.SetServiceProvider(false);
            await context.SaveChangesAsync();
        }

        Assert.Equal(0, await CountAsync());
    }

    [Fact]
    public async Task Profissional_Inativo_Nao_Conta()
    {
        await AddProfessionalAsync("Ativo", isServiceProvider: true);
        await AddProfessionalAsync("Inativo", isServiceProvider: true);

        await using (var context = _database.CreateContext(ContextFor(TenantId)))
        {
            var inactive = await context.Professionals.SingleAsync(p => p.Name == "Inativo");
            inactive.Deactivate();
            await context.SaveChangesAsync();
        }

        Assert.Equal(1, await CountAsync());
    }
}
