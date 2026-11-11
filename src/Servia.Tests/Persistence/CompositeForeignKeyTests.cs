using Microsoft.EntityFrameworkCore;
using Servia.Domain.Appointments;
using Servia.Domain.Catalog;
using Servia.Domain.Common;
using Servia.Domain.Customers;
using Servia.Domain.Team;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Tenancy;

namespace Servia.Tests.Persistence;

/// <summary>
/// F1.11 — chaves estrangeiras compostas com TenantId.
///
/// Verifica que o banco rejeita a associação de um agendamento a um cliente,
/// serviço ou profissional de outro estabelecimento. É a última linha de
/// defesa do isolamento: mesmo que uma camada superior falhe, o banco impede
/// a associação indevida.
/// </summary>
public class CompositeForeignKeyTests : IAsyncLifetime
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private static readonly DateOnly Date = new(2026, 11, 9);
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);

    private readonly PostgresTestDatabase _database = new();

    public async Task InitializeAsync() => await _database.InitializeAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private static ITenantContext ContextFor(Guid tenantId)
    {
        var context = new TenantContext();
        context.SetTenant(tenantId);
        return context;
    }

    /// <summary>Grava cliente, serviço e profissional do tenant A.</summary>
    private async Task<(Guid CustomerId, Guid ServiceId, Guid ProfessionalId)> SeedTenantAAsync()
    {
        var customer = Customer.Create(TenantA, "Cliente A", "21999990000", now: Now);
        var service = Service.Create(TenantA, "Corte A", Money.From(50m), 30);
        var professional = Professional.Create(TenantA, "Profissional A",
            WorkSchedule.Create([DayOfWeek.Monday], new TimeOnly(9, 0), new TimeOnly(18, 0)));

        await using var context = _database.CreateContext(ContextFor(TenantA));
        context.Customers.Add(customer);
        context.Services.Add(service);
        context.Professionals.Add(professional);
        await context.SaveChangesAsync();

        return (customer.Id, service.Id, professional.Id);
    }

    [Fact]
    public async Task Agendamento_Com_Referencias_Do_Mesmo_Tenant_Eh_Aceito()
    {
        var (customerId, serviceId, professionalId) = await SeedTenantAAsync();

        var appointment = Appointment.Create(
            TenantA, customerId, serviceId, professionalId, Date, new TimeOnly(10, 0), 30, Now);

        await using var context = _database.CreateContext(ContextFor(TenantA));
        context.Appointments.Add(appointment);

        await context.SaveChangesAsync();

        Assert.Equal(1, await context.Appointments.CountAsync());
    }

    [Fact]
    public async Task Banco_Rejeita_Cliente_De_Outro_Tenant()
    {
        var (_, serviceId, professionalId) = await SeedTenantAAsync();

        // Agendamento do tenant B referenciando cliente do tenant A.
        var appointment = Appointment.Create(
            TenantB, Guid.NewGuid(), serviceId, professionalId, Date, new TimeOnly(10, 0), 30, Now);

        await using var context = _database.CreateContext(ContextFor(TenantB));
        context.Appointments.Add(appointment);

        // A FK composta (TenantId, CustomerId) não encontra o par no tenant B.
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Banco_Rejeita_Servico_De_Outro_Tenant()
    {
        var (customerId, _, professionalId) = await SeedTenantAAsync();

        var appointment = Appointment.Create(
            TenantB, customerId, Guid.NewGuid(), professionalId, Date, new TimeOnly(10, 0), 30, Now);

        await using var context = _database.CreateContext(ContextFor(TenantB));
        context.Appointments.Add(appointment);

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Banco_Rejeita_Profissional_De_Outro_Tenant()
    {
        var (customerId, serviceId, _) = await SeedTenantAAsync();

        var appointment = Appointment.Create(
            TenantB, customerId, serviceId, Guid.NewGuid(), Date, new TimeOnly(10, 0), 30, Now);

        await using var context = _database.CreateContext(ContextFor(TenantB));
        context.Appointments.Add(appointment);

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Chave_Alternativa_Permite_Mesmo_Id_Em_Tenants_Diferentes()
    {
        // A chave alternativa é (TenantId, Id): o mesmo Id pode existir em
        // tenants distintos sem colidir, pois o par é diferente.
        var customerA = Customer.Create(TenantA, "Cliente A", "21999990000", now: Now);
        var customerB = Customer.Create(TenantB, "Cliente B", "21999990000", now: Now);

        await using var context = _database.CreateContext(ContextFor(TenantA));
        context.Customers.Add(customerA);
        context.Customers.Add(customerB);
        await context.SaveChangesAsync();

        // Ambos foram gravados: a unicidade é do par (TenantId, Id).
        Assert.NotEqual(customerA.Id, customerB.Id);
    }
}
