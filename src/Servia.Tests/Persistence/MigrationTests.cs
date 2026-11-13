using Microsoft.EntityFrameworkCore;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;
using Servia.Infrastructure.Tenancy;

namespace Servia.Tests.Persistence;

/// <summary>
/// F11.1 — migrations do EF Core.
/// F11.3 — seed de planos.
/// F11.4 — índices e chaves estrangeiras no schema.
/// F11.5 — verificação do schema gerado por migrations.
///
/// Aplica as migrations em um banco real e verifica o resultado. Não usa
/// EnsureCreated: o schema precisa ser o mesmo que vai para produção.
/// </summary>
public class MigrationTests : IAsyncLifetime
{
    private readonly PostgresTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private AppDbContext CreateContext() => _database.CreateContext();

    // ---------- F11.1: migrations ----------

    [Fact]
    public async Task Migrations_Aplicam_Sem_Erro()
    {
        await using var context = CreateContext();

        Assert.True(await context.Database.CanConnectAsync());
    }

    [Fact]
    public async Task Migrations_Sao_Idempotentes()
    {
        await using var context = CreateContext();

        await context.Database.MigrateAsync();

        // Aplicar duas vezes nao lanca nem duplica.
        Assert.True(await context.Database.CanConnectAsync());
    }

    [Fact]
    public async Task Nao_Ha_Migration_Pendente()
    {
        await using var context = CreateContext();

        var pending = await context.Database.GetPendingMigrationsAsync();

        // Se houver migration pendente, o modelo mudou sem migration.
        Assert.Empty(pending);
    }

    // ---------- F11.4: schema ----------

    [Fact]
    public async Task Schema_Contem_As_Tabelas_Principais()
    {
        await using var context = CreateContext();

        var tables = await GetTablesAsync(context);

        string[] expected =
        [
            "Tenants", "Plans", "Subscriptions", "Users", "RefreshTokens",
            "Appointments", "Services", "Professionals", "Customers",
            "Sites", "PlatformInvoices", "UsageQuotas", "Payments",
            "Transactions", "CommissionEntries", "Conversations",
            "MembershipPlans", "LoyaltyAccounts", "AiOperationLogs"
        ];

        foreach (var table in expected)
        {
            Assert.Contains(table, tables);
        }
    }

    [Fact]
    public async Task Indice_Critico_De_Agenda_Existe()
    {
        await using var context = CreateContext();

        var indexes = await GetIndexesAsync(context, "Appointments");

        Assert.Contains(indexes, name =>
            name.Contains("TenantId", StringComparison.OrdinalIgnoreCase)
            && name.Contains("ProfessionalId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Indice_Unico_De_Subdominio_Existe()
    {
        await using var context = CreateContext();

        var indexes = await GetIndexesAsync(context, "Sites");

        Assert.Contains(indexes, name =>
            name.Contains("Subdomain", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Indice_Unico_De_Email_Por_Tenant_Existe()
    {
        await using var context = CreateContext();

        var indexes = await GetIndexesAsync(context, "Users");

        Assert.Contains(indexes, name =>
            name.Contains("TenantId", StringComparison.OrdinalIgnoreCase)
            && name.Contains("Email", StringComparison.OrdinalIgnoreCase));
    }

    // ---------- F11.3: seed de planos ----------

    [Fact]
    public async Task Seed_Cria_Os_Quatro_Planos()
    {
        await using var context = CreateContext();

        var created = await PlanSeeder.SeedAsync(context);

        Assert.Equal(4, created);

        var codes = await context.Plans.Select(plan => plan.Code).ToListAsync();

        Assert.Contains("ESSENCIAL", codes);
        Assert.Contains("PRO", codes);
        Assert.Contains("PREMIUM", codes);
        Assert.Contains("BUSINESS", codes);
    }

    [Fact]
    public async Task Seed_Eh_Idempotente()
    {
        await using var context = CreateContext();

        await PlanSeeder.SeedAsync(context);
        var secondRun = await PlanSeeder.SeedAsync(context);

        Assert.Equal(0, secondRun);
        Assert.Equal(4, await context.Plans.CountAsync());
    }

    [Fact]
    public async Task Planos_Tem_As_Faixas_Definidas()
    {
        await using var context = CreateContext();
        await PlanSeeder.SeedAsync(context);

        var essential = await context.Plans.FirstAsync(plan => plan.Code == "ESSENCIAL");
        var business = await context.Plans.FirstAsync(plan => plan.Code == "BUSINESS");

        Assert.Equal(3, essential.IncludedProfessionals);
        Assert.True(business.HasUnlimitedProfessionals);
        Assert.Equal(SiteCustomizationLevel.Basic, essential.CustomizationLevel);
        Assert.Equal(SiteCustomizationLevel.Full, business.CustomizationLevel);
    }

    [Fact]
    public async Task Planos_Tem_Os_Dados_Comerciais_Definidos()
    {
        // Seção 4.1: o backend é a fonte única de verdade dos dados dos
        // planos. Este teste fixa os valores que o frontend consome pela API.
        await using var context = CreateContext();
        await PlanSeeder.SeedAsync(context);

        var plans = await context.Plans.ToDictionaryAsync(plan => plan.Code);

        Assert.Equal(99m, plans["ESSENCIAL"].MonthlyPrice.Amount);
        Assert.Equal(3, plans["ESSENCIAL"].IncludedProfessionals);
        Assert.Equal(100, plans["ESSENCIAL"].IncludedAiUnits);

        Assert.Equal(199m, plans["PRO"].MonthlyPrice.Amount);
        Assert.Equal(7, plans["PRO"].IncludedProfessionals);
        Assert.Equal(500, plans["PRO"].IncludedAiUnits);

        Assert.Equal(299m, plans["PREMIUM"].MonthlyPrice.Amount);
        Assert.Equal(15, plans["PREMIUM"].IncludedProfessionals);
        Assert.Equal(1500, plans["PREMIUM"].IncludedAiUnits);

        Assert.Equal(499m, plans["BUSINESS"].MonthlyPrice.Amount);
        Assert.True(plans["BUSINESS"].HasUnlimitedProfessionals);
        Assert.Equal(5000, plans["BUSINESS"].IncludedAiUnits);
    }

    [Fact]
    public async Task Seed_Nao_Sobrescreve_Plano_Existente()
    {
        // Seção 4.2: a atualização dos planos é controlada e preserva os
        // registros existentes (e, por consequência, assinaturas/históricos).
        await using var context = CreateContext();
        await PlanSeeder.SeedAsync(context);

        var essential = await context.Plans.FirstAsync(plan => plan.Code == "ESSENCIAL");
        essential.ChangePrice(Servia.Domain.Common.Money.From(123m));
        await context.SaveChangesAsync();

        await PlanSeeder.SeedAsync(context);

        var reloaded = await context.Plans.FirstAsync(plan => plan.Code == "ESSENCIAL");
        Assert.Equal(123m, reloaded.MonthlyPrice.Amount);
    }

    // ---------- Auxiliares ----------

    private static async Task<List<string>> GetTablesAsync(AppDbContext context) =>
        await context.Database
            .SqlQuery<string>($"SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public'")
            .ToListAsync();

    private static async Task<List<string>> GetIndexesAsync(AppDbContext context, string table) =>
        await context.Database
            .SqlQuery<string>($"SELECT indexname AS \"Value\" FROM pg_indexes WHERE tablename = {table}")
            .ToListAsync();
}
