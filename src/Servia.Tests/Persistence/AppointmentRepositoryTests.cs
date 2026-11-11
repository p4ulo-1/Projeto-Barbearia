using Microsoft.EntityFrameworkCore;
using Servia.Domain.Appointments;
using Servia.Domain.Catalog;
using Servia.Domain.Common;
using Servia.Domain.Customers;
using Servia.Domain.Tenancy;
using Servia.Domain.Team;
using Servia.Infrastructure.Repositories;
using Servia.Infrastructure.Tenancy;

namespace Servia.Tests.Persistence;

/// <summary>
/// Testes de persistência com banco real (SQLite em memória).
///
/// Verificam que o mapeamento funciona, que os objetos de valor sobrevivem ao
/// ciclo de gravação e leitura, e que o isolamento por tenant é respeitado na
/// consulta. Critério B04: associação com multiplicidade implementada.
/// </summary>
public class AppointmentRepositoryTests : IAsyncLifetime
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid OtherTenantId = Guid.NewGuid();
    // IDs reais das entidades criadas no seed. A FK composta exige que o
    // agendamento referencie entidades existentes no mesmo tenant.
    private readonly Dictionary<Guid, (Guid Customer, Guid Service, Guid Professional)> _refs = [];
    private static readonly DateOnly Date = new(2026, 11, 9);
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);

    private readonly PostgresTestDatabase _database = new();

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();

        // O filtro global exige tenant resolvido: sem ele, nada e visivel.
        _database.DefaultTenantId = TenantId;
        _database.TenantContext.SetTenant(TenantId);

        // Cria as entidades referenciadas antes de qualquer agendamento.
        await SeedReferencesAsync(TenantId);
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private Appointment NewAppointment(
        Guid? tenantId = null,
        Guid? professionalId = null,
        TimeOnly? time = null,
        int duration = 30)
    {
        var tenant = tenantId ?? TenantId;
        var (customer, service, professional) = _refs[tenant];

        return Appointment.Create(
            tenant,
            customer,
            service,
            professionalId ?? professional,
            Date,
            time ?? new TimeOnly(10, 0),
            duration,
            Now);
    }

    /// <summary>
    /// Grava as entidades referenciadas pelo agendamento.
    /// A FK composta (TenantId, Id) exige que elas existam no mesmo tenant.
    /// </summary>
    private async Task SeedReferencesAsync(Guid tenantId)
    {
        await using var context = _database.CreateContext(ContextFor(tenantId));

        // Os IDs precisam coincidir com os usados pelo agendamento.
        var customer = Customer.Create(tenantId, "Cliente", "21999990000", now: Now);
        var service = Service.Create(tenantId, "Servico", Money.From(50m), 30);
        var professional = Professional.Create(tenantId, "Profissional",
            WorkSchedule.Create([DayOfWeek.Monday], new TimeOnly(9, 0), new TimeOnly(18, 0)));

        context.Customers.Add(customer);
        context.Services.Add(service);
        context.Professionals.Add(professional);

        await context.SaveChangesAsync();

        _refs[tenantId] = (customer.Id, service.Id, professional.Id);
    }

    private static ITenantContext ContextFor(Guid tenantId)
    {
        var context = new TenantContext();
        context.SetTenant(tenantId);
        return context;
    }

    // ---------- Gravação e leitura ----------

    [Fact]
    public async Task Adicionar_E_Recuperar_Agendamento()
    {
        var appointment = NewAppointment();

        await using (var context = _database.CreateContext())
        {
            var repository = new AppointmentRepository(context);
            await repository.AddAsync(appointment);
            await repository.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            var repository = new AppointmentRepository(context);
            var loaded = await repository.GetByIdAsync(TenantId, appointment.Id);

            Assert.NotNull(loaded);
            Assert.Equal(appointment.Id, loaded!.Id);
            Assert.Equal(AppointmentStatus.Confirmed, loaded.Status);
            Assert.Equal(Date, loaded.Date);
            Assert.Equal(new TimeOnly(10, 0), loaded.Time);
            Assert.Equal(30, loaded.DurationMinutes);
        }
    }

    [Fact]
    public async Task Estado_Cancelado_Persiste_Com_Motivo()
    {
        var appointment = NewAppointment();
        appointment.Cancel("Cliente desistiu");

        await using (var context = _database.CreateContext())
        {
            var repository = new AppointmentRepository(context);
            await repository.AddAsync(appointment);
            await repository.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            var repository = new AppointmentRepository(context);
            var loaded = await repository.GetByIdAsync(TenantId, appointment.Id);

            Assert.Equal(AppointmentStatus.Cancelled, loaded!.Status);
            Assert.Equal("Cliente desistiu", loaded.CancellationReason);
        }
    }

    // ---------- Consulta de agenda ----------

    [Fact]
    public async Task Consulta_Retorna_Apenas_Agendamentos_Confirmados()
    {
        var confirmed = NewAppointment(time: new TimeOnly(10, 0));
        var cancelled = NewAppointment(time: new TimeOnly(11, 0));
        cancelled.Cancel();

        await using (var context = _database.CreateContext())
        {
            var repository = new AppointmentRepository(context);
            await repository.AddAsync(confirmed);
            await repository.AddAsync(cancelled);
            await repository.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            var repository = new AppointmentRepository(context);
            var active = await repository.GetActiveByProfessionalAndDateAsync(
                TenantId, _refs[TenantId].Professional, Date);

            Assert.Single(active);
            Assert.Equal(confirmed.Id, active[0].Id);
        }
    }

    // ---------- Isolamento por tenant ----------

    [Fact]
    public async Task Consulta_Nao_Retorna_Agendamento_De_Outro_Tenant()
    {
        await SeedReferencesAsync(OtherTenantId);

        var mine = NewAppointment();
        var other = NewAppointment(tenantId: OtherTenantId);

        await using (var context = _database.CreateContext())
        {
            var repository = new AppointmentRepository(context);
            await repository.AddAsync(mine);
            await repository.AddAsync(other);
            await repository.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            var repository = new AppointmentRepository(context);
            var active = await repository.GetActiveByProfessionalAndDateAsync(
                TenantId, _refs[TenantId].Professional, Date);

            Assert.Single(active);
            Assert.Equal(mine.Id, active[0].Id);
        }
    }

    [Fact]
    public async Task Busca_Por_Id_Nao_Atravessa_Tenant()
    {
        var appointment = NewAppointment();

        await using (var context = _database.CreateContext())
        {
            var repository = new AppointmentRepository(context);
            await repository.AddAsync(appointment);
            await repository.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            var repository = new AppointmentRepository(context);
            var loaded = await repository.GetByIdAsync(OtherTenantId, appointment.Id);

            Assert.Null(loaded);
        }
    }

    // ---------- Objetos de valor e composição ----------

    [Fact]
    public async Task Money_Sobrevive_Ao_Ciclo_De_Persistencia()
    {
        var service = Service.Create(TenantId, "Corte", Money.From(75.50m), 45);

        await using (var context = _database.CreateContext())
        {
            context.Services.Add(service);
            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            var loaded = await context.Services.FirstAsync(item => item.Id == service.Id);

            Assert.Equal(75.50m, loaded.Price.Amount);
            Assert.Equal("BRL", loaded.Price.Currency);
            Assert.Equal(45, loaded.DurationMinutes);
        }
    }

    [Fact]
    public async Task Jornada_Composta_Sobrevive_Ao_Ciclo_De_Persistencia()
    {
        var schedule = WorkSchedule.Create(
            [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday],
            new TimeOnly(9, 0),
            new TimeOnly(18, 0),
            slotIntervalMinutes: 30);

        var professional = Professional.Create(TenantId, "Ana Souza", schedule, title: "Cabeleireira");

        await using (var context = _database.CreateContext())
        {
            context.Professionals.Add(professional);
            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            var loaded = await context.Professionals.FirstAsync(item => item.Id == professional.Id);

            Assert.Equal(new TimeOnly(9, 0), loaded.Schedule.StartTime);
            Assert.Equal(new TimeOnly(18, 0), loaded.Schedule.EndTime);
            Assert.Equal(30, loaded.Schedule.SlotIntervalMinutes);
            Assert.Equal(3, loaded.Schedule.WorkDays.Count);
            Assert.Contains(DayOfWeek.Monday, loaded.Schedule.WorkDays);
            Assert.Contains(DayOfWeek.Friday, loaded.Schedule.WorkDays);
            Assert.DoesNotContain(DayOfWeek.Tuesday, loaded.Schedule.WorkDays);
        }
    }

    [Fact]
    public async Task Cliente_Anonimizado_Persiste_Sem_Dados_Pessoais()
    {
        var customer = Customer.Create(
            TenantId, "João das Coves", "21999998888",
            email: "joao@exemplo.com",
            birthDate: new DateOnly(1990, 5, 20),
            now: Now);

        customer.Anonymize();

        await using (var context = _database.CreateContext())
        {
            context.Customers.Add(customer);
            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            var loaded = await context.Customers.FirstAsync(item => item.Id == customer.Id);

            Assert.Equal("Cliente anonimizado", loaded.Name);
            Assert.Null(loaded.Email);
            Assert.Null(loaded.BirthDate);
            Assert.True(loaded.IsAnonymized);
        }
    }

    // ---------- Índice crítico ----------

    [Fact]
    public async Task Indice_De_Agenda_Existe_No_Schema()
    {
        await using var context = _database.CreateContext();

        var indexes = await context.Database
            .SqlQuery<string>($"SELECT indexname AS \"Value\" FROM pg_indexes WHERE tablename = 'Appointments'")
            .ToListAsync();

        Assert.Contains(indexes, name => name.Contains("TenantId", StringComparison.OrdinalIgnoreCase)
                                         && name.Contains("ProfessionalId", StringComparison.OrdinalIgnoreCase));
    }
}
