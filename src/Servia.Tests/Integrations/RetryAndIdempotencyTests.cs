using Servia.Infrastructure.Integrations;

using Servia.Tests.Common;

namespace Servia.Tests.Integrations;

/// <summary>
/// F12.4, F12.5 — idempotência de webhooks.
/// F12.6 — fila de envio com retry.
/// </summary>
public class RetryAndIdempotencyTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    // ---------- F12.4, F12.5: idempotencia ----------

    [Fact]
    public void Primeira_Chave_Eh_Registrada()
    {
        var store = new InMemoryIdempotencyStore(new FixedTimeProvider(Now));

        Assert.True(store.TryRegister("evento-1"));
    }

    [Fact]
    public void Chave_Repetida_Nao_Eh_Registrada_De_Novo()
    {
        var store = new InMemoryIdempotencyStore(new FixedTimeProvider(Now));

        Assert.True(store.TryRegister("evento-1"));
        Assert.False(store.TryRegister("evento-1"));
    }

    [Fact]
    public void Chaves_Diferentes_Sao_Independentes()
    {
        var store = new InMemoryIdempotencyStore(new FixedTimeProvider(Now));

        Assert.True(store.TryRegister("evento-1"));
        Assert.True(store.TryRegister("evento-2"));
    }

    [Fact]
    public void Contains_Reflete_O_Estado()
    {
        var store = new InMemoryIdempotencyStore(new FixedTimeProvider(Now));

        Assert.False(store.Contains("evento-1"));

        store.TryRegister("evento-1");

        Assert.True(store.Contains("evento-1"));
    }

    [Fact]
    public void Chave_Vazia_Lanca_Excecao()
    {
        var store = new InMemoryIdempotencyStore(new FixedTimeProvider(Now));

        Assert.Throws<ArgumentException>(() => store.TryRegister("  "));
    }

    [Fact]
    public void Chave_Expira_Apos_A_Janela_De_Retencao()
    {
        var time = new FixedTimeProvider(Now);
        var store = new InMemoryIdempotencyStore(time);

        store.TryRegister("evento-1");

        // Avanca alem da janela: a chave e descartada.
        time.Advance(InMemoryIdempotencyStore.Retention + TimeSpan.FromHours(1));

        Assert.True(store.TryRegister("evento-1"));
    }

    // ---------- F12.6: retry ----------

    [Theory]
    [InlineData(408, true)]
    [InlineData(429, true)]
    [InlineData(500, true)]
    [InlineData(503, true)]
    [InlineData(400, false)]
    [InlineData(401, false)]
    [InlineData(404, false)]
    public void Classifica_Falhas_Transitorias(int statusCode, bool expected)
    {
        Assert.Equal(expected, RetryPolicy.IsTransient(statusCode));
    }

    [Fact]
    public void Falha_De_Rede_Eh_Transitoria()
    {
        Assert.True(RetryPolicy.IsTransient(null));
    }

    [Fact]
    public void Backoff_Cresce_Exponencialmente()
    {
        var policy = new RetryPolicy(baseDelay: TimeSpan.FromSeconds(1), jitter: () => 0);

        Assert.Equal(1000, policy.DelayFor(1).TotalMilliseconds);
        Assert.Equal(2000, policy.DelayFor(2).TotalMilliseconds);
        Assert.Equal(4000, policy.DelayFor(3).TotalMilliseconds);
    }

    [Fact]
    public void Jitter_Adiciona_Variacao()
    {
        var policy = new RetryPolicy(baseDelay: TimeSpan.FromSeconds(1), jitter: () => 1.0);

        // Jitter maximo: 25% acima do valor base.
        Assert.Equal(1250, policy.DelayFor(1).TotalMilliseconds);
    }

    [Fact]
    public async Task Executa_Com_Sucesso_Na_Primeira_Tentativa()
    {
        var policy = new RetryPolicy(baseDelay: TimeSpan.FromMilliseconds(1));
        var attempts = 0;

        var result = await policy.ExecuteAsync(
            _ => { attempts++; return Task.FromResult("ok"); },
            _ => false);

        Assert.Equal("ok", result);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task Repete_Quando_A_Falha_Eh_Transitoria()
    {
        var policy = new RetryPolicy(maxAttempts: 3, baseDelay: TimeSpan.FromMilliseconds(1));
        var attempts = 0;

        var result = await policy.ExecuteAsync(
            _ =>
            {
                attempts++;
                return Task.FromResult(attempts < 3 ? 503 : 200);
            },
            statusCode => RetryPolicy.IsTransient(statusCode));

        Assert.Equal(200, result);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task Nao_Repete_Quando_A_Falha_Eh_Permanente()
    {
        var policy = new RetryPolicy(maxAttempts: 3, baseDelay: TimeSpan.FromMilliseconds(1));
        var attempts = 0;

        var result = await policy.ExecuteAsync(
            _ => { attempts++; return Task.FromResult(400); },
            statusCode => RetryPolicy.IsTransient(statusCode));

        Assert.Equal(400, result);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task Lanca_Quando_Todas_As_Tentativas_Falham()
    {
        var policy = new RetryPolicy(maxAttempts: 2, baseDelay: TimeSpan.FromMilliseconds(1));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            policy.ExecuteAsync<int>(
                _ => throw new HttpRequestException("falha de rede"),
                _ => true));
    }

    [Fact]
    public void Numero_De_Tentativas_Invalido_Lanca_Excecao()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryPolicy(maxAttempts: 0));
    }
}

/// <summary>Provedor de tempo controlável.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan delta) => _now = _now.Add(delta);
}
