using Servia.Infrastructure.Integrations;
using Servia.Infrastructure.Persistence;
using Servia.Infrastructure.Tenancy;
using Servia.Tests.Common;
using Servia.Tests.Integration;

namespace Servia.Tests.Persistence;

/// <summary>
/// Limite de agendamento público persistido no PostgreSQL (C3).
///
/// <b>Prova:</b> o contador é compartilhado entre instâncias (dois contextos
/// distintos sobre o mesmo banco), a atualização é atômica sob concorrência,
/// a janela expira e o isolamento por estabelecimento/origem é respeitado.
/// </summary>
public sealed class PublicBookingThrottlePersistenceTests : IAsyncLifetime
{
    private readonly PostgresTestDatabase _database = new();

    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public async Task InitializeAsync() => await _database.InitializeAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    private static PublicBookingThrottleOptions Options(int max = 3, int windowMinutes = 10) =>
        new() { MaxAttempts = max, WindowMinutes = windowMinutes };

    private PostgresPublicBookingThrottle Throttle(AppDbContext context, TimeProvider clock, PublicBookingThrottleOptions options) =>
        new(context, options, clock);

    private AppDbContext NewContext()
    {
        var tenantContext = new TenantContext();
        return _database.CreateContext(tenantContext);
    }

    [Fact]
    public async Task Permite_Ate_O_Limite_E_Depois_Nega()
    {
        var tenantId = Guid.NewGuid();
        var clock = new FixedTimeProvider(Now);

        await using var context = NewContext();
        var throttle = Throttle(context, clock, Options(max: 3));

        Assert.True(await throttle.TryAcquireAsync(tenantId, "1.2.3.4"));
        Assert.True(await throttle.TryAcquireAsync(tenantId, "1.2.3.4"));
        Assert.True(await throttle.TryAcquireAsync(tenantId, "1.2.3.4"));
        Assert.False(await throttle.TryAcquireAsync(tenantId, "1.2.3.4"));
    }

    [Fact]
    public async Task Contador_E_Compartilhado_Entre_Instancias()
    {
        var tenantId = Guid.NewGuid();
        var clock = new FixedTimeProvider(Now);
        var options = Options(max: 2);

        // Instancia A consome o limite.
        await using (var contextA = NewContext())
        {
            var throttleA = Throttle(contextA, clock, options);
            Assert.True(await throttleA.TryAcquireAsync(tenantId, "1.2.3.4"));
            Assert.True(await throttleA.TryAcquireAsync(tenantId, "1.2.3.4"));
        }

        // Instancia B (outro contexto) enxerga o mesmo contador e nega.
        await using (var contextB = NewContext())
        {
            var throttleB = Throttle(contextB, clock, options);
            Assert.False(await throttleB.TryAcquireAsync(tenantId, "1.2.3.4"));
        }
    }

    [Fact]
    public async Task Atualizacoes_Concorrentes_Nao_Ultrapassam_O_Limite()
    {
        var tenantId = Guid.NewGuid();
        var clock = new FixedTimeProvider(Now);
        var options = Options(max: 5);

        // 20 tentativas concorrentes, cada uma com seu proprio contexto
        // (simulando instancias distintas). No maximo 5 devem ser aceitas.
        var tasks = Enumerable.Range(0, 20).Select(async _ =>
        {
            await using var context = NewContext();
            var throttle = Throttle(context, clock, options);
            return await throttle.TryAcquireAsync(tenantId, "9.9.9.9");
        });

        var results = await Task.WhenAll(tasks);

        Assert.Equal(5, results.Count(accepted => accepted));
    }

    [Fact]
    public async Task Janela_Expirada_Recomeca_A_Contagem()
    {
        var tenantId = Guid.NewGuid();
        var clock = new MutableTimeProvider(Now);
        var options = Options(max: 2, windowMinutes: 10);

        await using var context = NewContext();
        var throttle = Throttle(context, clock, options);

        Assert.True(await throttle.TryAcquireAsync(tenantId, "1.2.3.4"));
        Assert.True(await throttle.TryAcquireAsync(tenantId, "1.2.3.4"));
        Assert.False(await throttle.TryAcquireAsync(tenantId, "1.2.3.4"));

        // Avanca para a proxima janela fixa.
        clock.Advance(TimeSpan.FromMinutes(11));

        Assert.True(await throttle.TryAcquireAsync(tenantId, "1.2.3.4"));
    }

    [Fact]
    public async Task Isolamento_Por_Estabelecimento_E_Origem()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var clock = new FixedTimeProvider(Now);
        var options = Options(max: 1);

        await using var context = NewContext();
        var throttle = Throttle(context, clock, options);

        Assert.True(await throttle.TryAcquireAsync(tenantA, "1.2.3.4"));
        Assert.False(await throttle.TryAcquireAsync(tenantA, "1.2.3.4"));

        // Outro estabelecimento e outra origem nao sao afetados.
        Assert.True(await throttle.TryAcquireAsync(tenantB, "1.2.3.4"));
        Assert.True(await throttle.TryAcquireAsync(tenantA, "5.6.7.8"));
    }

    [Fact]
    public async Task Limpeza_Remove_Janelas_Expiradas()
    {
        var tenantId = Guid.NewGuid();
        var clock = new FixedTimeProvider(Now);

        await using var context = NewContext();
        var throttle = Throttle(context, clock, Options());

        await throttle.TryAcquireAsync(tenantId, "1.2.3.4");

        // Limpa tudo que expirou antes de um instante futuro.
        var removed = await throttle.CleanupExpiredAsync(Now.AddHours(1));

        Assert.True(removed >= 1);
    }
}
