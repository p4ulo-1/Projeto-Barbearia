using System.Net;
using Servia.Infrastructure.Integrations;

namespace Servia.Api.Middleware;

/// <summary>
/// Resolve a origem real da requisição para o rate limiting (C3).
///
/// <b>Nunca confiar cegamente no X-Forwarded-For:</b> o cabeçalho é
/// controlado pelo cliente. Só é considerado quando a conexão imediata vem
/// de um proxy declarado como confiável (<c>PublicBooking:Throttle:TrustedProxies</c>).
/// Caso contrário, usa o IP da conexão.
///
/// Quando há proxy confiável, o primeiro IP da cadeia é o cliente original;
/// os demais são proxies. Um cliente não consegue forjar a origem porque o
/// proxy confiável anexa o IP real à cadeia.
/// </summary>
public static class ClientOriginResolver
{
    public static string Resolve(
        HttpContext context,
        PublicBookingThrottleOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);

        var remote = context.Connection.RemoteIpAddress;

        if (remote is null)
        {
            return "desconhecido";
        }

        var trusted = options.TrustedProxies ?? [];

        // Sem proxy confiavel configurado, ou conexao direta de origem nao
        // confiavel: usa o IP da conexao. O X-Forwarded-For e ignorado.
        if (trusted.Length == 0 || !IsTrusted(remote, trusted))
        {
            return remote.ToString();
        }

        var forwarded = context.Request.Headers["X-Forwarded-For"].ToString();

        if (string.IsNullOrWhiteSpace(forwarded))
        {
            return remote.ToString();
        }

        // Primeiro IP da cadeia = cliente original.
        var first = forwarded
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(first) ? remote.ToString() : first;
    }

    private static bool IsTrusted(IPAddress address, IReadOnlyList<string> trusted)
    {
        foreach (var entry in trusted)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                continue;
            }

            if (IPAddress.TryParse(entry, out var exact) && exact.Equals(address))
            {
                return true;
            }

            // Suporte a faixa CIDR simples (a.b.c.d/nn).
            if (entry.Contains('/', StringComparison.Ordinal)
                && IPNetwork.TryParse(entry, out var network)
                && network.Contains(address))
            {
                return true;
            }
        }

        return false;
    }
}