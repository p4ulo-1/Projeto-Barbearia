namespace Servia.Infrastructure.Integrations;

/// <summary>
/// Configuração do limite de agendamento público (C3).
///
/// Os valores vêm da configuração (<c>PublicBooking:Throttle</c>), não de
/// constantes espalhadas. Cada ambiente ajusta os limites sem alterar código.
/// </summary>
public sealed class PublicBookingThrottleOptions
{
    /// <summary>Máximo de agendamentos por (estabelecimento, origem) na janela.</summary>
    public int MaxAttempts { get; set; } = 10;

    /// <summary>Duração da janela, em minutos.</summary>
    public int WindowMinutes { get; set; } = 10;

    /// <summary>
    /// Proxies confiáveis (IPs/CIDRs) cujo <c>X-Forwarded-For</c> é aceito.
    /// Vazio significa: não confiar em cabeçalhos encaminhados; usa o IP da
    /// conexão. Nunca confiar indiscriminadamente no <c>X-Forwarded-For</c>.
    /// </summary>
    public string[] TrustedProxies { get; set; } = [];
}
