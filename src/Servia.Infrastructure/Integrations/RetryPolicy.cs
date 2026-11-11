namespace Servia.Infrastructure.Integrations;

/// <summary>
/// Política de retry com backoff exponencial (F12.6).
///
/// Falhas temporárias de integração — timeout, 429, 503 — devem ser
/// reprocessadas. Falhas permanentes — 400, 401, 404 — não devem: repetir
/// não muda o resultado e só consome cota.
///
/// O backoff é exponencial com jitter, para que várias instâncias não
/// retentem no mesmo instante e derrubem o serviço externo.
/// </summary>
public sealed class RetryPolicy
{
    /// <summary>Máximo de tentativas, incluindo a primeira.</summary>
    public const int DefaultMaxAttempts = 3;

    /// <summary>Atraso base entre tentativas.</summary>
    public static readonly TimeSpan DefaultBaseDelay = TimeSpan.FromSeconds(1);

    private readonly int _maxAttempts;
    private readonly TimeSpan _baseDelay;
    private readonly Func<double> _jitter;

    public RetryPolicy(
        int maxAttempts = DefaultMaxAttempts,
        TimeSpan? baseDelay = null,
        Func<double>? jitter = null)
    {
        if (maxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxAttempts), "O número de tentativas deve ser maior que zero.");
        }

        _maxAttempts = maxAttempts;
        _baseDelay = baseDelay ?? DefaultBaseDelay;
        _jitter = jitter ?? Random.Shared.NextDouble;
    }

    public int MaxAttempts => _maxAttempts;

    /// <summary>
    /// Indica se a falha é temporária e vale a pena repetir.
    /// </summary>
    public static bool IsTransient(int? statusCode) => statusCode switch
    {
        null => true,           // falha de rede ou timeout
        408 => true,            // request timeout
        429 => true,            // rate limit
        >= 500 => true,         // erro do servidor
        _ => false              // 4xx: erro do cliente, repetir nao ajuda
    };

    /// <summary>
    /// Calcula o atraso antes da tentativa informada (1-based).
    /// </summary>
    public TimeSpan DelayFor(int attempt)
    {
        if (attempt <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attempt), "A tentativa deve ser maior que zero.");
        }

        // Backoff exponencial: 1s, 2s, 4s...
        var exponential = _baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1);

        // Jitter de ate 25% para evitar sincronizacao entre instancias.
        var jitterFactor = 1 + (_jitter() * 0.25);

        return TimeSpan.FromMilliseconds(exponential * jitterFactor);
    }

    /// <summary>
    /// Executa a operação com retry, respeitando a política.
    /// Lança a última exceção quando todas as tentativas falham.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        Func<T, bool> shouldRetry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(shouldRetry);

        Exception? lastException = null;

        for (var attempt = 1; attempt <= _maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var result = await operation(cancellationToken);

                if (!shouldRetry(result) || attempt == _maxAttempts)
                {
                    return result;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                lastException = exception;

                if (attempt == _maxAttempts)
                {
                    throw;
                }
            }

            await Task.Delay(DelayFor(attempt), cancellationToken);
        }

        throw lastException ?? new InvalidOperationException("A operação falhou sem exceção.");
    }
}
