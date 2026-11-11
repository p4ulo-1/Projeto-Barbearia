namespace Servia.Domain.Tenancy;

/// <summary>
/// Limite de cadastros por origem (F21.3).
///
/// Sem isso, um script criaria milhares de tenants em segundos. O limite
/// é por IP e por janela de tempo.
/// </summary>
public interface ISignupThrottle
{
    /// <summary>
    /// Registra uma tentativa. Retorna falso quando o limite foi excedido.
    /// </summary>
    bool TryAcquire(string clientKey);
}
