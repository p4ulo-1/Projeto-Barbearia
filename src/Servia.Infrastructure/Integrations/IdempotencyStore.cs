using System.Collections.Concurrent;

namespace Servia.Infrastructure.Integrations;

/// <summary>
/// Registro de chaves de idempotência já processadas (F12.4, F12.5).
///
/// Webhooks de gateway e de mensageria podem ser entregues mais de uma vez.
/// Sem idempotência, um evento repetido geraria cobrança ou mensagem
/// duplicada ao cliente.
///
/// A implementação em memória serve para uma instância. Em produção com
/// múltiplas instâncias, a mesma interface deve ser implementada sobre o
/// banco ou um cache distribuído.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Registra a chave. Retorna falso quando ela já foi processada.
    /// </summary>
    bool TryRegister(string key);

    /// <summary>Indica se a chave já foi processada.</summary>
    bool Contains(string key);
}

/// <summary>Implementação em memória, com expiração por tempo.</summary>
public sealed class InMemoryIdempotencyStore(TimeProvider timeProvider) : IIdempotencyStore
{
    /// <summary>Janela de retenção das chaves processadas.</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    private readonly ConcurrentDictionary<string, DateTimeOffset> _processed = new(StringComparer.Ordinal);

    public bool TryRegister(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("A chave de idempotência é obrigatória.", nameof(key));
        }

        var now = timeProvider.GetUtcNow();
        PurgeExpired(now);

        return _processed.TryAdd(key, now);
    }

    public bool Contains(string key) =>
        !string.IsNullOrWhiteSpace(key) && _processed.ContainsKey(key);

    private void PurgeExpired(DateTimeOffset now)
    {
        foreach (var entry in _processed)
        {
            if (now - entry.Value > Retention)
            {
                _processed.TryRemove(entry.Key, out _);
            }
        }
    }
}
