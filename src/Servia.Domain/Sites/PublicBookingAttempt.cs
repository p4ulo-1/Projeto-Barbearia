namespace Servia.Domain.Sites;

/// <summary>
/// Contador persistente de tentativas de agendamento público (C3).
///
/// <b>Por que persistir:</b> o limite em memória não é compartilhado entre
/// instâncias — um atacante contorna o limite distribuindo requisições. O
/// contador vive no PostgreSQL, que já é a infraestrutura do projeto.
///
/// <b>Chave:</b> (estabelecimento, origem). O abuso contra um site não
/// consome a cota de outro, nem a de um visitante legítimo de outro IP.
///
/// <b>Janela fixa:</b> cada linha representa uma janela de tempo. Ao expirar,
/// a contagem recomeça. A atualização é atômica no banco (ver
/// <c>PostgresPublicBookingThrottle</c>), evitando corrida entre instâncias.
/// </summary>
public sealed class PublicBookingAttempt
{
    private PublicBookingAttempt(
        Guid id,
        Guid tenantId,
        string clientKey,
        int count,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd)
    {
        Id = id;
        TenantId = tenantId;
        ClientKey = clientKey;
        Count = count;
        WindowStart = windowStart;
        WindowEnd = windowEnd;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>.
    /// </summary>
    private PublicBookingAttempt()
    {
        ClientKey = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>Origem da requisição (IP confiável), normalizada.</summary>
    public string ClientKey { get; private set; }

    /// <summary>Tentativas contabilizadas na janela corrente.</summary>
    public int Count { get; private set; }

    public DateTimeOffset WindowStart { get; private set; }

    /// <summary>Fim da janela. Após este instante, a contagem recomeça.</summary>
    public DateTimeOffset WindowEnd { get; private set; }

    public static PublicBookingAttempt Create(
        Guid tenantId,
        string clientKey,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd)
    {
        if (tenantId == Guid.Empty)
        {
            throw new Common.DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(clientKey))
        {
            throw new Common.DomainRuleException("A origem da requisição é obrigatória.");
        }

        if (windowEnd <= windowStart)
        {
            throw new Common.DomainRuleException("A janela de tempo é inválida.");
        }

        return new PublicBookingAttempt(
            Guid.NewGuid(),
            tenantId,
            clientKey.Trim(),
            count: 0,
            windowStart,
            windowEnd);
    }
}