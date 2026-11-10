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
public class MigrationTests : IDisposable
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"servia-migration-{Guid.NewGuid():N}.db");

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_databasePath}")
            .Options;

        return new AppDbContext(options, new TenantContext());
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        try
        {
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }
        catch (IOException)
        {
            // Arquivo temporario: remocao best-effort.
        }
    }

    // ---------- F11.1: migrations ----------

    [Fact]
    public async Task Migrations_Aplicam_Sem_Erro()
    {
        await using var context = CreateContext();

        await context.Database.MigrateAsync();

        Assert.True(await context.Database.CanConnectAsync());
    }

    [Fact]
    public async Task Migrations_Sao_Idempotentes()
    {
        await using var context = CreateContext();

        await context.Database.MigrateAsync();
        await context.Database.MigrateAsync();

        // Aplicar duas vezes nao lanca nem duplica.
        Assert.True(await context.Database.CanConnectAsync());
    }

    [Fact]
    public async Task Nao_Ha_Migration_Pendente()
    {
        await using var context = CreateContext();

        await context.Database.MigrateAsync();

        var pending = await context.Database.GetPendingMigrationsAsync();

        // Se houver migration pendente, o modelo mudou sem migration.
        Assert.Empty(pending);
    }

    // ---------- F11.4: schema ----------

    [Fact]
    public async Task Schema_Contem_As_Tabelas_Principais()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

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
        await context.Database.MigrateAsync();

        var indexes = await GetIndexesAsync(context, "Appointments");

        Assert.Contains(indexes, name =>
            name.Contains("TenantId", StringComparison.OrdinalIgnoreCase)
            && name.Contains("ProfessionalId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Indice_Unico_De_Subdominio_Existe()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        var indexes = await GetIndexesAsync(context, "Sites");

        Assert.Contains(indexes, name =>
            name.Contains("Subdomain", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Indice_Unico_De_Email_Por_Tenant_Existe()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

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
        await context.Database.MigrateAsync();

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
        await context.Database.MigrateAsync();

        await PlanSeeder.SeedAsync(context);
        var secondRun = await PlanSeeder.SeedAsync(context);

        Assert.Equal(0, secondRun);
        Assert.Equal(4, await context.Plans.CountAsync());
    }

    [Fact]
    public async Task Planos_Tem_As_Faixas_Definidas()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        await PlanSeeder.SeedAsync(context);

        var essential = await context.Plans.FirstAsync(plan => plan.Code == "ESSENCIAL");
        var business = await context.Plans.FirstAsync(plan => plan.Code == "BUSINESS");

        Assert.Equal(3, essential.IncludedProfessionals);
        Assert.True(business.HasUnlimitedProfessionals);
        Assert.Equal(SiteCustomizationLevel.Basic, essential.CustomizationLevel);
        Assert.Equal(SiteCustomizationLevel.Full, business.CustomizationLevel);
    }

    // ---------- Auxiliares ----------

    private static async Task<List<string>> GetTablesAsync(AppDbContext context) =>
        await context.Database
            .SqlQuery<string>($"SELECT name AS Value FROM sqlite_master WHERE type = 'table'")
            .ToListAsync();

    private static async Task<List<string>> GetIndexesAsync(AppDbContext context, string table) =>
        await context.Database
            .SqlQuery<string>($"SELECT name AS Value FROM sqlite_master WHERE type = 'index' AND tbl_name = {table}")
            .ToListAsync();
}
