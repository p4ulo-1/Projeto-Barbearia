using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Operations;

/// <summary>
/// Registro de um evento de webhook já processado (F17.4).
///
/// Provedores reenviam eventos quando não recebem confirmação. Sem
/// idempotência persistida, um reenvio geraria cobrança ou mensagem
/// duplicada ao cliente.
///
/// <b>Por que no banco:</b> a implementação em memória se perde no
/// reinício do processo, e o provedor reenvia justamente quando o
/// processo caiu.
/// </summary>
public sealed class ProcessedEvent : ITenantScoped
{
    /// <summary>Janela de retenção das chaves processadas.</summary>
    public const int RetentionDays = 7;

    private ProcessedEvent(
        Guid id,
        Guid tenantId,
        string eventId,
        string source,
        DateTimeOffset processedAt)
    {
        Id = id;
        TenantId = tenantId;
        EventId = eventId;
        Source = source;
        ProcessedAt = processedAt;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Record"/>.
    /// </summary>
    private ProcessedEvent()
    {
        EventId = string.Empty;
        Source = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>Identificador do evento no provedor.</summary>
    public string EventId { get; private set; }

    /// <summary>Origem do evento: `mercadopago` ou `whatsapp`.</summary>
    public string Source { get; private set; }

    public DateTimeOffset ProcessedAt { get; private set; }

    /// <summary>Indica se o registro pode ser removido pela retenção.</summary>
    public bool IsExpired(DateTimeOffset now) =>
        now - ProcessedAt > TimeSpan.FromDays(RetentionDays);

    /// <summary>
    /// Registra um evento processado.
    /// </summary>
    public static ProcessedEvent Record(
        Guid tenantId,
        string eventId,
        string source,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(eventId))
        {
            throw new DomainRuleException("O identificador do evento é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            throw new DomainRuleException("A origem do evento é obrigatória.");
        }

        return new ProcessedEvent(
            Guid.NewGuid(), tenantId, eventId.Trim(), source.Trim().ToLowerInvariant(), now);
    }
}
