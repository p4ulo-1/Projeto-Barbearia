using System.Collections.Concurrent;
using Servia.Domain.Sites;

namespace Servia.Infrastructure.Integrations;

/// <summary>
/// Limite em memória de agendamentos públicos (C3).
///
/// Mantido para testes unitários determinísticos e como fallback simples.
/// Em produção usa-se <see cref="PostgresPublicBookingThrottle"/>, que é
/// compartilhado entre instâncias.
/// </summary>
public sealed class InMemoryPublicBookingThrottle(
    TimeProvider timeProvider,
    PublicBookingThrottleOptions? options = null) : IPublicBookingThrottle
{
    private readonly int _maxAttempts = Math.Max(1, options?.MaxAttempts ?? 10);
    private readonly TimeSpan _window = TimeSpan.FromMinutes(Math.Max(1, options?.WindowMinutes ?? 10));

    private readonly ConcurrentDictionary<string, List<DateTimeOffset>> _attempts =
        new(StringComparer.Ordinal);

    public Task<bool> TryAcquireAsync(
        Guid tenantId,
        string clientKey,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(clientKey))
        {
            // Sem identificacao de origem, nao ha como limitar: rejeita.
            return Task.FromResult(false);
        }

        var key = $"{tenantId:N}:{clientKey.Trim()}";
        var now = timeProvider.GetUtcNow();
        var attempts = _attempts.GetOrAdd(key, _ => []);

        lock (attempts)
        {
            attempts.RemoveAll(attempt => now - attempt > _window);

            if (attempts.Count >= _maxAttempts)
            {
                return Task.FromResult(false);
            }

            attempts.Add(now);

            return Task.FromResult(true);
        }
    }
}

/// <summary>
/// Limite sem efeito, usado em desenvolvimento e testes (C3).
///
/// Os testes de integração vêm todos do mesmo IP e seriam bloqueados,
/// impedindo validar o restante. Em produção o limite é obrigatório.
/// </summary>
public sealed class UnlimitedPublicBookingThrottle : IPublicBookingThrottle
{
    public Task<bool> TryAcquireAsync(
        Guid tenantId,
        string clientKey,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(true);
}
