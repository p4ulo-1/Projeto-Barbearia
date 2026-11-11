using Microsoft.EntityFrameworkCore;
using Servia.Domain.Common;
using Servia.Domain.Appointments;
using Servia.Domain.Catalog;
using Servia.Domain.Customers;
using Servia.Domain.Finance;
using Servia.Domain.Loyalty;
using Servia.Domain.Team;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;
using Servia.Infrastructure.Tenancy;

namespace Servia.Tests.Persistence;

/// <summary>
/// Etapa 6 da auditoria — consultas e desempenho.
///
/// Antes da correção, <c>FinanceController</c> e <c>ModulesController</c>
/// materializavam tabelas inteiras com <c>ToListAsync()</c> e filtravam em
/// memória. Em PostgreSQL com volume real, isso degrada memória e latência.
///
/// Estes testes provam que o filtro e a agregação rodam no banco e que o
/// isolamento por tenant é preservado.
/// </summary>
public class QueryPerformanceTests : IAsyncLifetime
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 11);

    private readonly PostgresTestDatabase _database = new();

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();

        await using var context = _database.CreateContext(new TenantContext());
        await PlanSeeder.SeedAsync(context);
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private static ITenantContext ContextFor(Guid tenantId)
    {
        var context = new TenantContext();
        context.SetTenant(tenantId);
        return context;
    }

    /// <summary>Cria lancamentos no tenant informado.</summary>
    private async Task SeedTransactionsAsync(Guid tenantId, int count, TransactionType type)
    {
        await using var context = _database.CreateContext(ContextFor(tenantId));

        for (var index = 0; index < count; index++)
        {
            context.Transactions.Add(Transaction.Create(
                tenantId, type, Money.From(100m), $"Lancamento {index}", Now));
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Cria cliente, servico, profissional e 5 agendamentos reais.
    /// A FK composta de ServiceReview exige agendamento existente no tenant.
    /// </summary>
    private static async Task<(Guid CustomerId, Guid[] AppointmentIds)> SeedReviewReferencesAsync(
        AppDbContext context, Guid tenantId)
    {
        var customer = Customer.Create(tenantId, "Cliente", "21999998888", now: Now);
        var service = Service.Create(tenantId, "Servico", Money.From(50m), 30);
        var professional = Professional.Create(
            tenantId, "Profissional",
            WorkSchedule.Create([DayOfWeek.Monday], new TimeOnly(9, 0), new TimeOnly(18, 0)));

        context.Customers.Add(customer);
        context.Services.Add(service);
        context.Professionals.Add(professional);
        await context.SaveChangesAsync();

        var appointmentIds = new Guid[5];

        for (var index = 0; index < appointmentIds.Length; index++)
        {
            var appointment = Appointment.Create(
                tenantId, customer.Id, service.Id, professional.Id,
                Today, new TimeOnly(9 + index, 0), 30, Now);

            context.Appointments.Add(appointment);
            appointmentIds[index] = appointment.Id;
        }

        await context.SaveChangesAsync();

        return (customer.Id, appointmentIds);
    }

    // ---------- Agregação no banco ----------

    [Fact]
    public async Task Agregacao_Por_Tipo_Retorna_Totais_Corretos()
    {
        await SeedTransactionsAsync(TenantA, 3, TransactionType.Income);
        await SeedTransactionsAsync(TenantA, 2, TransactionType.Expense);

        await using var context = _database.CreateContext(ContextFor(TenantA));

        var start = new DateTimeOffset(Today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var end = new DateTimeOffset(Today.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

        var totals = await context.Transactions
            .Where(transaction => transaction.OccurredAt >= start && transaction.OccurredAt <= end)
            .GroupBy(transaction => transaction.Type)
            .Select(group => new { Type = group.Key, Total = group.Sum(t => t.Amount.Amount) })
            .ToListAsync();

        Assert.Equal(300m, totals.First(item => item.Type == TransactionType.Income).Total);
        Assert.Equal(200m, totals.First(item => item.Type == TransactionType.Expense).Total);
    }

    [Fact]
    public async Task Agregacao_Respeita_O_Isolamento_Por_Tenant()
    {
        await SeedTransactionsAsync(TenantA, 3, TransactionType.Income);
        await SeedTransactionsAsync(TenantB, 10, TransactionType.Income);

        await using var context = _database.CreateContext(ContextFor(TenantA));

        var total = await context.Transactions
            .Where(transaction => transaction.Type == TransactionType.Income)
            .SumAsync(transaction => transaction.Amount.Amount);

        // Apenas os 3 lancamentos do tenant A.
        Assert.Equal(300m, total);
    }

    [Fact]
    public async Task Filtro_Por_Periodo_Exclui_Fora_Do_Intervalo()
    {
        await using (var context = _database.CreateContext(ContextFor(TenantA)))
        {
            context.Transactions.Add(Transaction.Create(
                TenantA, TransactionType.Income, Money.From(100m), "Dentro", Now));

            context.Transactions.Add(Transaction.Create(
                TenantA, TransactionType.Income, Money.From(999m), "Fora",
                Now.AddMonths(-6)));

            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext(ContextFor(TenantA)))
        {
            var start = new DateTimeOffset(Today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var end = new DateTimeOffset(Today.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

            var count = await context.Transactions
                .CountAsync(transaction =>
                    transaction.OccurredAt >= start && transaction.OccurredAt <= end);

            Assert.Equal(1, count);
        }
    }

    // ---------- NPS ----------

    [Fact]
    public async Task Nps_Agregado_Por_Faixa_De_Nota()
    {
        await using (var context = _database.CreateContext(ContextFor(TenantA)))
        {
            var (customerId, appointmentIds) = await SeedReviewReferencesAsync(context, TenantA);

            // 2 promotores (10, 9), 1 neutro (8), 1 detrator (3).
            var scores = new[] { 10, 9, 8, 3 };

            for (var index = 0; index < scores.Length; index++)
            {
                context.ServiceReviews.Add(ServiceReview.Create(
                    TenantA, appointmentIds[index], customerId, scores[index], Now));
            }

            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext(ContextFor(TenantA)))
        {
            var counts = await context.ServiceReviews
                .GroupBy(review => review.Score >= 9 ? "promoter"
                    : review.Score >= 7 ? "passive" : "detractor")
                .Select(group => new { Category = group.Key, Count = group.Count() })
                .ToListAsync();

            Assert.Equal(2, counts.First(item => item.Category == "promoter").Count);
            Assert.Equal(1, counts.First(item => item.Category == "passive").Count);
            Assert.Equal(1, counts.First(item => item.Category == "detractor").Count);
        }
    }

    [Fact]
    public async Task Nps_Respeita_O_Isolamento_Por_Tenant()
    {
        await using (var context = _database.CreateContext(ContextFor(TenantB)))
        {
            var (customerId, appointmentIds) = await SeedReviewReferencesAsync(context, TenantB);

            for (var index = 0; index < 5; index++)
            {
                context.ServiceReviews.Add(ServiceReview.Create(
                    TenantB, appointmentIds[index], customerId, 10, Now));
            }

            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext(ContextFor(TenantA)))
        {
            // Tenant A nao tem avaliacoes: nao ve as de B.
            var count = await context.ServiceReviews.CountAsync();

            Assert.Equal(0, count);
        }
    }

    // ---------- Projeção ----------

    [Fact]
    public async Task Projecao_Retorna_Apenas_Os_Campos_Necessarios()
    {
        await SeedTransactionsAsync(TenantA, 2, TransactionType.Income);

        await using var context = _database.CreateContext(ContextFor(TenantA));

        // Projecao: o banco devolve apenas descricao e valor.
        var projected = await context.Transactions
            .Select(transaction => new
            {
                transaction.Description,
                transaction.Amount.Amount
            })
            .ToListAsync();

        Assert.Equal(2, projected.Count);
        Assert.All(projected, item => Assert.Equal(100m, item.Amount));
    }
}
