using Servia.Infrastructure.Integrations;
using Servia.Tests.Common;
using Servia.Tests.Integration;

namespace Servia.Tests.Sites;

/// <summary>
/// Limite de agendamento público — comportamento em memória (C3).
///
/// Verifica a janela, o isolamento entre estabelecimentos e origens e a
/// rejeição de origem desconhecida. A versão persistida (PostgreSQL) é
/// coberta em <c>PublicBookingThrottlePersistenceTests</c>.
/// </summary>
public sealed class PublicBookingThrottleTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static PublicBookingThrottleOptions Options(int max = 3, int windowMinutes = 10) =>
        new() { MaxAttempts = max, WindowMinutes = windowMinutes };

    [Fact]
    public async Task Permite_Ate_O_Limite_Na_Janela()
    {
        var throttle = new InMemoryPublicBookingThrottle(new FixedTimeProvider(Now), Options());
        var tenantId = Guid.NewGuid();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            Assert.True(await throttle.TryAcquireAsync(tenantId, "1.2.3.4"));
        }

        Assert.False(await throttle.TryAcquireAsync(tenantId, "1.2.3.4"));
    }

    [Fact]
    public async Task Limite_E_Isolado_Por_Estabelecimento()
    {
        var throttle = new InMemoryPublicBookingThrottle(new FixedTimeProvider(Now), Options());
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await throttle.TryAcquireAsync(tenantA, "1.2.3.4");
        }

        // O outro estabelecimento nao e afetado pelo abuso contra o primeiro.
        Assert.True(await throttle.TryAcquireAsync(tenantB, "1.2.3.4"));
    }

    [Fact]
    public async Task Limite_E_Isolado_Por_Origem()
    {
        var throttle = new InMemoryPublicBookingThrottle(new FixedTimeProvider(Now), Options());
        var tenantId = Guid.NewGuid();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await throttle.TryAcquireAsync(tenantId, "1.2.3.4");
        }

        Assert.False(await throttle.TryAcquireAsync(tenantId, "1.2.3.4"));
        Assert.True(await throttle.TryAcquireAsync(tenantId, "5.6.7.8"));
    }

    [Fact]
    public async Task Origem_Desconhecida_E_Rejeitada()
    {
        var throttle = new InMemoryPublicBookingThrottle(new FixedTimeProvider(Now), Options());

        Assert.False(await throttle.TryAcquireAsync(Guid.NewGuid(), string.Empty));
        Assert.False(await throttle.TryAcquireAsync(Guid.Empty, "1.2.3.4"));
    }

    [Fact]
    public async Task Janela_Expirada_Libera_Novamente()
    {
        var clock = new MutableTimeProvider(Now);
        var throttle = new InMemoryPublicBookingThrottle(clock, Options(max: 2, windowMinutes: 10));
        var tenantId = Guid.NewGuid();

        Assert.True(await throttle.TryAcquireAsync(tenantId, "1.2.3.4"));
        Assert.True(await throttle.TryAcquireAsync(tenantId, "1.2.3.4"));
        Assert.False(await throttle.TryAcquireAsync(tenantId, "1.2.3.4"));

        // Passa da janela: a contagem recomeca.
        clock.Advance(TimeSpan.FromMinutes(11));

        Assert.True(await throttle.TryAcquireAsync(tenantId, "1.2.3.4"));
    }
}
