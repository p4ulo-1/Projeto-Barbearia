using System.Collections.Concurrent;
using Servia.Domain.Tenancy;

namespace Servia.Infrastructure.Integrations;

/// <summary>
/// Limite de cadastros por origem (F21.3).
///
/// Sem isso, um script criaria milhares de tenants em segundos — cada um
/// com trial de 14 dias e um usuário proprietário.
///
/// <b>Janela deslizante:</b> conta as tentativas dos últimos 10 minutos.
/// Uma janela fixa permitiria o dobro do limite na virada.
///
/// <b>Limitação conhecida:</b> a implementação é em memória, então o
/// limite é por instância. Com múltiplas instâncias, o limite efetivo é
/// multiplicado. Para produção com escala horizontal, a mesma interface
/// deve ser implementada sobre cache distribuído.
/// </summary>
public sealed class InMemorySignupThrottle(TimeProvider timeProvider) : ISignupThrottle
{
    /// <summary>Máximo de cadastros por origem na janela.</summary>
    public const int MaxAttempts = 5;

    /// <summary>Duração da janela deslizante.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, List<DateTimeOffset>> _attempts =
        new(StringComparer.Ordinal);

    public bool TryAcquire(string clientKey)
    {
        if (string.IsNullOrWhiteSpace(clientKey))
        {
            // Sem identificacao de origem, nao ha como limitar: rejeita.
            return false;
        }

        var now = timeProvider.GetUtcNow();
        var attempts = _attempts.GetOrAdd(clientKey, _ => []);

        lock (attempts)
        {
            // Remove as tentativas fora da janela.
            attempts.RemoveAll(attempt => now - attempt > Window);

            if (attempts.Count >= MaxAttempts)
            {
                return false;
            }

            attempts.Add(now);

            return true;
        }
    }
}
