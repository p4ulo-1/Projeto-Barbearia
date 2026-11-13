using Microsoft.EntityFrameworkCore;
using Servia.Domain.Sites;
using Servia.Infrastructure.Persistence;

namespace Servia.Infrastructure.Integrations;

/// <summary>
/// Limite de agendamento público persistido no PostgreSQL (C3).
///
/// <b>Compartilhado:</b> o contador fica no banco, então várias instâncias
/// da API enxergam o mesmo valor. Abandona o limite em memória, que um
/// atacante contornava distribuindo requisições.
///
/// <b>Janela fixa:</b> cada tentativa pertence à janela cujo início é
/// <c>floor(agora / janela) * janela</c>. Ao mudar a janela, a contagem
/// recomeça — sem precisar apagar nada na hora.
///
/// <b>Atomicidade:</b> a atualização usa
/// <c>INSERT ... ON CONFLICT DO UPDATE ... RETURNING</c>. O PostgreSQL
/// serializa as concorrentes sobre a mesma linha: duas instâncias não
/// conseguem, juntas, passar do limite.
///
/// <b>Sem infraestrutura nova:</b> usa o PostgreSQL que o projeto já tem.
/// </summary>
public sealed class PostgresPublicBookingThrottle(
    AppDbContext context,
    PublicBookingThrottleOptions options,
    TimeProvider timeProvider) : IPublicBookingThrottle
{
    public async Task<bool> TryAcquireAsync(
        Guid tenantId,
        string clientKey,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(clientKey))
        {
            // Sem identificacao de origem, nao ha como limitar: rejeita.
            return false;
        }

        var normalizedKey = clientKey.Trim();

        var window = TimeSpan.FromMinutes(Math.Max(1, options.WindowMinutes));
        var maxAttempts = Math.Max(1, options.MaxAttempts);

        var now = timeProvider.GetUtcNow();

        // Inicio da janela fixa atual.
        var ticks = now.UtcTicks - (now.UtcTicks % window.Ticks);
        var windowStart = new DateTimeOffset(ticks, TimeSpan.Zero);
        var windowEnd = windowStart + window;

        // Upsert atomico via comando direto. SqlQuery nao serve: ele envolve o
        // SQL como subconsulta, e um INSERT ... RETURNING nao e composavel.
        //
        // A chave unica (TenantId, ClientKey, WindowStart) garante uma linha
        // por janela; em conflito, incrementa. O COUNT retornado ja reflete o
        // incremento. Se passar do limite, devolvemos falso.
        var connection = context.Database.GetDbConnection();

        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();

        command.CommandText = """
            INSERT INTO "PublicBookingAttempts"
                ("Id", "TenantId", "ClientKey", "Count", "WindowStart", "WindowEnd")
            VALUES
                (@id, @tenantId, @clientKey, 1, @windowStart, @windowEnd)
            ON CONFLICT ("TenantId", "ClientKey", "WindowStart")
            DO UPDATE SET "Count" = "PublicBookingAttempts"."Count" + 1
            RETURNING "Count"
            """;

        AddParameter(command, "@id", Guid.NewGuid());
        AddParameter(command, "@tenantId", tenantId);
        AddParameter(command, "@clientKey", normalizedKey);
        AddParameter(command, "@windowStart", windowStart);
        AddParameter(command, "@windowEnd", windowEnd);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        var newCount = Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);

        return newCount <= maxAttempts;
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    /// <summary>
    /// Remove janelas já expiradas antes de <paramref name="before"/>.
    /// Pode ser chamada por um job periódico; mantém a tabela pequena.
    /// </summary>
    public async Task<int> CleanupExpiredAsync(
        DateTimeOffset before,
        CancellationToken cancellationToken = default)
    {
        return await context.PublicBookingAttempts
            .Where(attempt => attempt.WindowEnd < before)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
