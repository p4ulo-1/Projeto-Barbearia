namespace Servia.Domain.Sites;

/// <summary>
/// Limite de agendamentos públicos por origem (C3).
///
/// O endpoint de agendamento é anônimo: sem limite, um script cria clientes
/// e agendamentos em massa e esgota a agenda do estabelecimento. O limite é
/// POR ESTABELECIMENTO e POR ORIGEM, para que o abuso contra um site não
/// consuma a cota de outro nem a de um visitante legítimo.
///
/// <b>Por que assíncrono:</b> a implementação real persiste o contador no
/// PostgreSQL para ser compartilhada entre instâncias. A abstração não
/// conhece o banco — o domínio só declara o comportamento.
///
/// Separado de <c>ISignupThrottle</c> porque a semântica e os limites são
/// diferentes (cadastro de tenant não é agendamento).
/// </summary>
public interface IPublicBookingThrottle
{
    /// <summary>
    /// Registra uma tentativa de agendamento para a origem informada.
    /// Retorna falso quando o limite foi excedido.
    ///
    /// A contagem e a verificação são atômicas: duas requisições simultâneas
    /// da mesma origem não conseguem exceder o limite por corrida.
    /// </summary>
    Task<bool> TryAcquireAsync(
        Guid tenantId,
        string clientKey,
        CancellationToken cancellationToken = default);
}
