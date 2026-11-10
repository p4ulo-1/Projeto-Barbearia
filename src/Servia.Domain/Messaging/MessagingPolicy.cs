using Servia.Domain.Billing;
using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Messaging;

/// <summary>
/// Política de envio de mensagens por tenant (F6.9, F6.11–F6.13).
///
/// Concentra as decisões que controlam o envio:
///
///   - <b>Permissões por tipo</b> (F6.13): o estabelecimento decide quais
///     tipos de mensagem podem ser enviados.
///   - <b>Limite de consumo</b> (F6.9): o envio é bloqueado ao atingir a
///     cota, no servidor.
///   - <b>Franquia gratuita da Meta</b> (F6.11): repassada ao tenant, não
///     retida pela plataforma.
///   - <b>Teto do estabelecimento</b> (F6.12): além da franquia.
///
/// A verificação acontece <b>antes</b> do envio: mensagem bloqueada não
/// chega ao cliente e não consome cota.
/// </summary>
public sealed class MessagingPolicy : ITenantScoped
{
    private readonly HashSet<MessageType> _allowedTypes = [];

    private MessagingPolicy(Guid id, Guid tenantId)
    {
        Id = id;
        TenantId = tenantId;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>.
    /// </summary>
    private MessagingPolicy()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>Tipos de mensagem autorizados pelo estabelecimento.</summary>
    public IReadOnlyCollection<MessageType> AllowedTypes => _allowedTypes;

    public static MessagingPolicy Create(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        var policy = new MessagingPolicy(Guid.NewGuid(), tenantId);

        // Por padrão, os tipos operacionais são permitidos. Marketing é
        // opt-in: exige consentimento explícito do cliente.
        policy._allowedTypes.Add(MessageType.AppointmentConfirmation);
        policy._allowedTypes.Add(MessageType.AppointmentReminder);
        policy._allowedTypes.Add(MessageType.AppointmentChange);
        policy._allowedTypes.Add(MessageType.AutomatedReply);
        policy._allowedTypes.Add(MessageType.Manual);

        return policy;
    }

    /// <summary>Autoriza um tipo de mensagem (F6.13).</summary>
    public void AllowType(MessageType messageType) => _allowedTypes.Add(messageType);

    /// <summary>Revoga a autorização de um tipo de mensagem.</summary>
    public void DisallowType(MessageType messageType) => _allowedTypes.Remove(messageType);

    /// <summary>Indica se o tipo de mensagem está autorizado.</summary>
    public bool IsTypeAllowed(MessageType messageType) => _allowedTypes.Contains(messageType);

    /// <summary>
    /// Verifica se a mensagem pode ser enviada.
    /// Lança <see cref="DomainRuleException"/> quando o tipo não está
    /// autorizado ou a cota está esgotada.
    /// </summary>
    public void EnsureCanSend(MessageType messageType, UsageQuota quota)
    {
        ArgumentNullException.ThrowIfNull(quota);

        if (!IsTypeAllowed(messageType))
        {
            throw new DomainRuleException(
                $"O tipo de mensagem {messageType} não está autorizado para este estabelecimento.");
        }

        if (quota.Resource != QuotaResource.WhatsAppMessages)
        {
            throw new DomainRuleException("A cota informada não é de mensagens de WhatsApp.");
        }

        if (quota.Remaining <= 0)
        {
            throw new DomainRuleException(
                "O limite de mensagens foi atingido. Contrate créditos adicionais para continuar.");
        }
    }

    /// <summary>
    /// Consome uma mensagem da cota após verificar as permissões.
    /// Retorna falso quando o envio deve ser bloqueado.
    /// </summary>
    public bool TryConsumeForSend(MessageType messageType, UsageQuota quota)
    {
        ArgumentNullException.ThrowIfNull(quota);

        if (!IsTypeAllowed(messageType))
        {
            return false;
        }

        return quota.TryConsume(1);
    }
}
