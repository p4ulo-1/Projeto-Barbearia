using Microsoft.EntityFrameworkCore;
using Servia.Domain.Common;
using Servia.Domain.Identity;
using Servia.Infrastructure.Identity;
using Servia.Infrastructure.Persistence;
using Servia.Infrastructure.Provisioning;
using Servia.Infrastructure.Tenancy;
using Servia.Tests.Common;
using Servia.Tests.Persistence;

namespace Servia.Tests.Provisioning;

/// <summary>
/// F20.1 — criação do primeiro administrador da plataforma.
///
/// Sem isso, colocar o primeiro cliente no ar exige calcular um hash
/// PBKDF2 à mão e inserir no banco.
/// </summary>
public class PlatformAdminSeederTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 8, 0, 0, TimeSpan.Zero);
    private const string Password = "SenhaSegura123";

    private readonly PostgresTestDatabase _database = new();

    public async Task InitializeAsync() => await _database.InitializeAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private PlatformAdminSeeder NewSeeder(AppDbContext context) =>
        new(context, new Pbkdf2PasswordHasher(iterations: 1_000), new FixedTimeProvider(Now));

    [Fact]
    public async Task Cria_Administrador_Da_Plataforma()
    {
        await using var context = _database.CreateContext(new TenantContext());

        var created = await NewSeeder(context).SeedAsync("Suporte", "admin@servia.app", Password);

        Assert.Equal("admin@servia.app", created);
    }

    [Fact]
    public async Task Administrador_Criado_Tem_Papel_De_Plataforma()
    {
        await using var context = _database.CreateContext(new TenantContext());
        await NewSeeder(context).SeedAsync("Suporte", "admin@servia.app", Password);

        var admin = await context.Users
            .IgnoreQueryFilters()
            .FirstAsync(user => user.Email == "admin@servia.app");

        Assert.Equal(Role.PlatformAdmin, admin.Role);
        Assert.Equal(Guid.Empty, admin.TenantId);
    }

    [Fact]
    public async Task Senha_Eh_Armazenada_Com_Hash()
    {
        await using var context = _database.CreateContext(new TenantContext());
        await NewSeeder(context).SeedAsync("Suporte", "admin@servia.app", Password);

        var admin = await context.Users
            .IgnoreQueryFilters()
            .FirstAsync(user => user.Email == "admin@servia.app");

        Assert.DoesNotContain(Password, admin.PasswordHash, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Seed_Eh_Idempotente()
    {
        await using var context = _database.CreateContext(new TenantContext());
        var seeder = NewSeeder(context);

        var first = await seeder.SeedAsync("Suporte", "admin@servia.app", Password);
        var second = await seeder.SeedAsync("Outro", "outro@servia.app", Password);

        Assert.NotNull(first);
        Assert.Null(second);

        var count = await context.Users
            .IgnoreQueryFilters()
            .CountAsync(user => user.Role == Role.PlatformAdmin);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Senha_Curta_Lanca_Excecao()
    {
        await using var context = _database.CreateContext(new TenantContext());

        await Assert.ThrowsAsync<DomainRuleException>(() =>
            NewSeeder(context).SeedAsync("Suporte", "admin@servia.app", "123"));
    }

    [Fact]
    public async Task Email_Vazio_Lanca_Excecao()
    {
        await using var context = _database.CreateContext(new TenantContext());

        await Assert.ThrowsAsync<DomainRuleException>(() =>
            NewSeeder(context).SeedAsync("Suporte", "  ", Password));
    }

    [Fact]
    public async Task Administrador_Criado_Consegue_Autenticar()
    {
        await using var context = _database.CreateContext(new TenantContext());
        await NewSeeder(context).SeedAsync("Suporte", "admin@servia.app", Password);

        var hasher = new Pbkdf2PasswordHasher(iterations: 1_000);

        var admin = await context.Users
            .IgnoreQueryFilters()
            .FirstAsync(user => user.Email == "admin@servia.app");

        Assert.True(hasher.Verify(Password, admin.PasswordHash));
    }
}
