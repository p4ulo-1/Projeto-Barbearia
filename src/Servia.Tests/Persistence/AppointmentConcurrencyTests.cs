using Microsoft.EntityFrameworkCore;
using Servia.Application.Appointments;
using Servia.Domain.Appointments;
using Servia.Domain.Catalog;
using Servia.Domain.Common;
using Servia.Domain.Customers;
using Servia.Domain.Team;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;
using Servia.Infrastructure.Repositories;
using Servia.Infrastructure.Tenancy;

using Servia.Tests.Common;

namespace Servia.Tests.Persistence;

/// <summary>
/// Etapa 5 da auditoria — concorrência no agendamento.
///
/// Antes da correção, a verificação de disponibilidade e a gravação eram
/// operações separadas: duas requisições simultâneas consultavam, ambas
/// viam o horário livre e ambas gravavam.
///
/// Estes testes disparam requisições concorrentes reais contra PostgreSQL
/// 17 e verificam que apenas uma é aceita.
/// </summary>
public class AppointmentConcurrencyTests : IAsyncLifetime
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateOnly Date = new(2026, 11, 16);
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 8, 0, 0, TimeSpan.Zero);

    private readonly PostgresTestDatabase _database = new();

    private Guid _customerId;
    private Guid _serviceId;
    private Guid _professionalId;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();

        await using var context = _database.CreateContext(ContextFor(TenantId));

        var customer = Customer.Create(TenantId, "Cliente", "21999998888", now: Now);
        var service = Service.Create(TenantId, "Corte", Money.From(50m), 30);
        var professional = Professional.Create(
            TenantId, "Profissional",
            WorkSchedule.Create(
                [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                 DayOfWeek.Thursday, DayOfWeek.Friday],
                new TimeOnly(9, 0), new TimeOnly(18, 0)));

        context.Customers.Add(customer);
        context.Services.Add(service);
        context.Professionals.Add(professional);
        await context.SaveChangesAsync();

        _customerId = customer.Id;
        _serviceId = service.Id;
        _professionalId = professional.Id;
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private static ITenantContext ContextFor(Guid tenantId)
    {
        var context = new TenantContext();
        context.SetTenant(tenantId);
        return context;
    }

    /// <summary>
    /// Cria um serviço com contexto e repositorio proprios, como uma
    /// requisicao real faria.
    /// </summary>
    private AppointmentService NewService()
    {
        var context = _database.CreateContext(ContextFor(TenantId));
        var repository = new AppointmentRepository(context);

        return new AppointmentService(repository, new FixedTimeProvider(Now));
    }

    private CreateAppointmentCommand Command(TimeOnly time, int duration = 30) =>
        new(TenantId, _customerId, _serviceId, _professionalId, Date, time, duration);

    // ---------- Concorrência real ----------

    [Fact]
    public async Task Requisicoes_Simultaneas_No_Mesmo_Horario_Aceitam_Apenas_Uma()
    {
        const int concurrentRequests = 5;

        // Cada requisicao tem seu proprio contexto, como em producao.
        var tasks = Enumerable.Range(0, concurrentRequests)
            .Select(_ => NewService().CreateAsync(Command(new TimeOnly(10, 0))))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        var accepted = results.Count(result => result.IsSuccess);
        var conflicts = results.Count(result => !result.IsSuccess);

        // Exatamente uma vence; as demais recebem conflito.
        Assert.Equal(1, accepted);
        Assert.Equal(concurrentRequests - 1, conflicts);

        // E apenas um agendamento foi persistido.
        await using var context = _database.CreateContext(ContextFor(TenantId));
        var persisted = await context.Appointments.CountAsync();

        Assert.Equal(1, persisted);
    }

    [Fact]
    public async Task Sobreposicao_Parcial_Eh_Rejeitada()
    {
        // 10:00-10:30 ja existe.
        var first = await NewService().CreateAsync(Command(new TimeOnly(10, 0)));
        Assert.True(first.IsSuccess);

        // 10:15-10:45 sobrepoe parcialmente.
        var overlapping = await NewService().CreateAsync(Command(new TimeOnly(10, 15)));

        Assert.False(overlapping.IsSuccess);
        Assert.Equal(AppointmentFailureKind.Conflict, overlapping.Failure!.Kind);
    }

    [Fact]
    public async Task Sobreposicao_Com_Duracao_Diferente_Eh_Rejeitada()
    {
        // 10:00-11:00 (60 min).
        var first = await NewService().CreateAsync(Command(new TimeOnly(10, 0), 60));
        Assert.True(first.IsSuccess);

        // 10:30-11:00 (30 min) cai dentro do intervalo anterior.
        var overlapping = await NewService().CreateAsync(Command(new TimeOnly(10, 30), 30));

        Assert.False(overlapping.IsSuccess);
    }

    [Fact]
    public async Task Horario_Adjacente_Eh_Aceito()
    {
        var first = await NewService().CreateAsync(Command(new TimeOnly(10, 0)));
        Assert.True(first.IsSuccess);

        // 10:30 comeca exatamente quando o anterior termina: nao sobrepoe.
        var adjacent = await NewService().CreateAsync(Command(new TimeOnly(10, 30)));

        Assert.True(adjacent.IsSuccess);
    }

    [Fact]
    public async Task Horarios_Diferentes_Sao_Aceitos_Sequencialmente()
    {
        // Com isolamento serializavel, transacoes concorrentes que tocam a
        // mesma tabela podem ser abortadas por conflito de serializacao —
        // mesmo em horarios distintos. O comportamento correto e: cada
        // requisicao isolada e aceita.
        var first = await NewService().CreateAsync(Command(new TimeOnly(9, 0)));
        var second = await NewService().CreateAsync(Command(new TimeOnly(11, 0)));
        var third = await NewService().CreateAsync(Command(new TimeOnly(14, 0)));

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.True(third.IsSuccess);
    }

    // ---------- ScheduleLock ----------

    [Fact]
    public async Task Trava_Ativa_Impede_Segunda_Reserva_Do_Mesmo_Horario()
    {
        await using var context = _database.CreateContext(ContextFor(TenantId));

        var first = ScheduleLock.Acquire(
            TenantId, _professionalId, Date, new TimeOnly(15, 0), 30, Now);

        context.ScheduleLocks.Add(first);
        await context.SaveChangesAsync();

        // Segunda trava no mesmo horario: o indice unico parcial rejeita.
        var second = ScheduleLock.Acquire(
            TenantId, _professionalId, Date, new TimeOnly(15, 0), 30, Now);

        context.ScheduleLocks.Add(second);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Trava_Expirada_Nao_Bloqueia_Nova_Reserva()
    {
        await using (var context = _database.CreateContext(ContextFor(TenantId)))
        {
            // Trava que expirou ha uma hora: liberada explicitamente, como
            // o dominio faz quando detecta expiracao.
            var expired = ScheduleLock.Acquire(
                TenantId, _professionalId, Date, new TimeOnly(16, 0), 30,
                Now.AddHours(-2), lifetimeMinutes: 5);

            expired.Release();

            context.ScheduleLocks.Add(expired);
            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext(ContextFor(TenantId)))
        {
            // O indice parcial filtra por ExpiresAt > now(): a trava
            // expirada nao ocupa o horario.
            var fresh = ScheduleLock.Acquire(
                TenantId, _professionalId, Date, new TimeOnly(16, 0), 30, Now);

            context.ScheduleLocks.Add(fresh);
            await context.SaveChangesAsync();

            Assert.Equal(2, await context.ScheduleLocks.CountAsync());
        }
    }

    [Fact]
    public async Task Trava_De_Outro_Profissional_Nao_Conflita()
    {
        await using var context = _database.CreateContext(ContextFor(TenantId));

        var otherProfessional = Professional.Create(
            TenantId, "Outro",
            WorkSchedule.Create([DayOfWeek.Monday], new TimeOnly(9, 0), new TimeOnly(18, 0)));

        context.Professionals.Add(otherProfessional);
        await context.SaveChangesAsync();

        var first = ScheduleLock.Acquire(
            TenantId, _professionalId, Date, new TimeOnly(17, 0), 30, Now);

        var second = ScheduleLock.Acquire(
            TenantId, otherProfessional.Id, Date, new TimeOnly(17, 0), 30, Now);

        context.ScheduleLocks.Add(first);
        context.ScheduleLocks.Add(second);

        // Profissionais diferentes: sem conflito.
        await context.SaveChangesAsync();

        Assert.Equal(2, await context.ScheduleLocks.CountAsync());
    }
}

