using Microsoft.EntityFrameworkCore;
using Servia.Domain.Appointments;
using Servia.Domain.Catalog;
using Servia.Domain.Customers;
using Servia.Domain.Team;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Repositories;
using Servia.Infrastructure.Tenancy;

namespace Servia.Tests.Persistence;

/// <summary>
/// Testes de isolamento multi-tenant (F1.12).
///
/// Verificam que o filtro global do EF Core impede leitura, alteração e
/// exclusão de dados de outro tenant. Se o filtro for removido, estes testes
/// falham — é o que garante que a proteção não seja acidental.
/// </summary>
public class TenantIsolationTests : IDisposable
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    // IDs reais das entidades criadas no seed, por tenant.
    private readonly Dictionary<Guid, (Guid Customer, Guid Service, Guid Professional)> _refs = [];
    private static readonly DateOnly Date = new(2026, 11, 9);
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);

    private readonly SqliteTestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private Appointment NewAppointment(Guid tenantId, TimeOnly time)
    {
        var (customer, service, professional) = _refs[tenantId];
        return Appointment.Create(tenantId, customer, service, professional, Date, time, 30, Now);
    }

    private static ITenantContext ContextFor(Guid tenantId)
    {
        var context = new TenantContext();
        context.SetTenant(tenantId);
        return context;
    }

    /// <summary>
    /// Grava dados de dois tenants.
    /// As entidades referenciadas pelo agendamento sao criadas antes, pois a
    /// FK composta (TenantId, Id) exige que existam no mesmo tenant.
    /// </summary>
    private async Task SeedTwoTenantsAsync()
    {
        await using var context = _database.CreateContext(ContextFor(TenantA));

        var customerA = Customer.Create(TenantA, "Cliente A", "21999990000", now: Now);
        var customerB = Customer.Create(TenantB, "Cliente B", "21999990001", now: Now);
        var serviceA = Service.Create(TenantA, "Corte A", Domain.Common.Money.From(50m), 30);
        var serviceB = Service.Create(TenantB, "Corte B", Domain.Common.Money.From(60m), 30);
        var profA = Professional.Create(TenantA, "Prof A",
            WorkSchedule.Create([DayOfWeek.Monday], new TimeOnly(9, 0), new TimeOnly(18, 0)));
        var profB = Professional.Create(TenantB, "Prof B",
            WorkSchedule.Create([DayOfWeek.Monday], new TimeOnly(9, 0), new TimeOnly(18, 0)));

        context.Customers.Add(customerA);
        context.Customers.Add(customerB);
        context.Services.Add(serviceA);
        context.Services.Add(serviceB);
        context.Professionals.Add(profA);
        context.Professionals.Add(profB);

        await context.SaveChangesAsync();

        _refs[TenantA] = (customerA.Id, serviceA.Id, profA.Id);
        _refs[TenantB] = (customerB.Id, serviceB.Id, profB.Id);

        context.Appointments.Add(NewAppointment(TenantA, new TimeOnly(10, 0)));
        context.Appointments.Add(NewAppointment(TenantB, new TimeOnly(11, 0)));

        await context.SaveChangesAsync();
    }

    // ---------- Leitura ----------

    [Fact]
    public async Task Consulta_Retorna_Apenas_Dados_Do_Tenant_Corrente()
    {
        await SeedTwoTenantsAsync();

        await using var context = _database.CreateContext(ContextFor(TenantA));
        var appointments = await context.Appointments.ToListAsync();

        Assert.Single(appointments);
        Assert.All(appointments, item => Assert.Equal(TenantA, item.TenantId));
    }

    [Fact]
    public async Task Cada_Tenant_Ve_Apenas_Os_Proprios_Servicos()
    {
        await SeedTwoTenantsAsync();

        await using var contextA = _database.CreateContext(ContextFor(TenantA));
        var servicesA = await contextA.Services.ToListAsync();

        await using var contextB = _database.CreateContext(ContextFor(TenantB));
        var servicesB = await contextB.Services.ToListAsync();

        Assert.Single(servicesA);
        Assert.Equal("Corte A", servicesA[0].Name);

        Assert.Single(servicesB);
        Assert.Equal("Corte B", servicesB[0].Name);
    }

    [Fact]
    public async Task Repositorio_Nao_Retorna_Agendamento_De_Outro_Tenant()
    {
        await SeedTwoTenantsAsync();

        await using var context = _database.CreateContext(ContextFor(TenantB));
        var repository = new AppointmentRepository(context);

        var active = await repository.GetActiveByProfessionalAndDateAsync(
            TenantB, _refs[TenantB].Professional, Date);

        Assert.Single(active);
        Assert.Equal(TenantB, active[0].TenantId);
    }

    // ---------- Sem tenant resolvido ----------

    [Fact]
    public async Task Sem_Tenant_Resolvido_Nenhum_Dado_E_Retornado()
    {
        await SeedTwoTenantsAsync();

        // Contexto sem tenant: o filtro deve negar acesso, não liberar tudo.
        await using var context = _database.CreateContext(new TenantContext());
        var appointments = await context.Appointments.ToListAsync();

        Assert.Empty(appointments);
    }

    [Fact]
    public async Task Sem_Tenant_Resolvido_Servicos_Tambem_Ficam_Inacessiveis()
    {
        await SeedTwoTenantsAsync();

        await using var context = _database.CreateContext(new TenantContext());
        var services = await context.Services.ToListAsync();

        Assert.Empty(services);
    }

    // ---------- Alteracao ----------

    [Fact]
    public async Task Alteracao_Em_Dado_De_Outro_Tenant_Nao_E_Encontrada()
    {
        await SeedTwoTenantsAsync();

        // Tenant B tenta carregar o agendamento do tenant A pelo id.
        Guid appointmentOfA;

        await using (var contextA = _database.CreateContext(ContextFor(TenantA)))
        {
            appointmentOfA = (await contextA.Appointments.FirstAsync()).Id;
        }

        await using var contextB = _database.CreateContext(ContextFor(TenantB));
        var found = await contextB.Appointments.FirstOrDefaultAsync(item => item.Id == appointmentOfA);

        Assert.Null(found);
    }

    // ---------- Exclusao ----------

    [Fact]
    public async Task Exclusao_De_Dado_De_Outro_Tenant_Nao_Afeta_O_Registro()
    {
        await SeedTwoTenantsAsync();

        Guid appointmentOfA;

        await using (var contextA = _database.CreateContext(ContextFor(TenantA)))
        {
            appointmentOfA = (await contextA.Appointments.FirstAsync()).Id;
        }

        // Tenant B tenta remover o agendamento do tenant A.
        await using (var contextB = _database.CreateContext(ContextFor(TenantB)))
        {
            var target = await contextB.Appointments.FirstOrDefaultAsync(item => item.Id == appointmentOfA);

            if (target is not null)
            {
                contextB.Appointments.Remove(target);
                await contextB.SaveChangesAsync();
            }
        }

        // O registro do tenant A continua existindo.
        await using var verify = _database.CreateContext(ContextFor(TenantA));
        var stillThere = await verify.Appointments.FirstOrDefaultAsync(item => item.Id == appointmentOfA);

        Assert.NotNull(stillThere);
    }

    // ---------- Entidades globais ----------

    [Fact]
    public async Task Plano_Eh_Global_E_Visivel_Para_Todos_Os_Tenants()
    {
        var plan = Plan.Create("ESSENCIAL", "Essencial",
            Domain.Common.Money.From(99m), 3, SiteCustomizationLevel.Basic, 100);

        await using (var seed = _database.CreateContext(ContextFor(TenantA)))
        {
            seed.Plans.Add(plan);
            await seed.SaveChangesAsync();
        }

        // Plano não implementa ITenantScoped: é visível de qualquer tenant.
        await using var contextB = _database.CreateContext(ContextFor(TenantB));
        var visible = await contextB.Plans.FirstOrDefaultAsync(item => item.Id == plan.Id);

        Assert.NotNull(visible);
    }

    [Fact]
    public async Task Tenant_Nao_Eh_Filtrado_Por_Tenant()
    {
        var tenantA = Tenant.Create("barbearia-a", "Barbearia A", SegmentType.Barbershop, "111", Now);
        var tenantB = Tenant.Create("barbearia-b", "Barbearia B", SegmentType.HairSalon, "222", Now);

        await using (var seed = _database.CreateContext(ContextFor(TenantA)))
        {
            seed.Tenants.Add(tenantA);
            seed.Tenants.Add(tenantB);
            await seed.SaveChangesAsync();
        }

        // Tenant é a raiz do agregado: não tem filtro por TenantId.
        await using var context = _database.CreateContext(ContextFor(TenantA));
        var tenants = await context.Tenants.ToListAsync();

        Assert.Equal(2, tenants.Count);
    }
}
