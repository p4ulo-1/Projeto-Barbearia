using Servia.Domain.Common;

namespace Servia.Domain.Messaging;

/// <summary>
/// Operação do WhatsApp como Solution Partner da Meta (F6.10).
///
/// O Servia centraliza a integração e o pagamento à Meta. O estabelecimento
/// não contrata a Meta diretamente: consome o serviço operado.
///
/// A operação depende de dois pré-requisitos externos:
///
///   1. <b>Aprovação</b> como Solution Partner.
///   2. <b>Linha de crédito</b> para pagar o consumo à Meta.
///
/// Sem ambos, o envio de mensagens não pode entrar em produção. O estado é
/// explícito para que a falha seja visível, não silenciosa.
/// </summary>
public sealed class WhatsAppPartnerOperation
{
    private WhatsAppPartnerOperation(
        bool isApproved,
        bool hasCreditLine,
        DateTimeOffset? approvedAt)
    {
        IsApproved = isApproved;
        HasCreditLine = hasCreditLine;
        ApprovedAt = approvedAt;
    }

    /// <summary>Indica se o Servia foi aprovado como Solution Partner.</summary>
    public bool IsApproved { get; private set; }

    /// <summary>Indica se há linha de crédito ativa junto à Meta.</summary>
    public bool HasCreditLine { get; private set; }

    public DateTimeOffset? ApprovedAt { get; private set; }

    /// <summary>
    /// Indica se a operação pode entrar em produção.
    /// Exige aprovação <b>e</b> linha de crédito.
    /// </summary>
    public bool CanOperate => IsApproved && HasCreditLine;

    /// <summary>Cria a operação em estado não aprovado.</summary>
    public static WhatsAppPartnerOperation NotApproved() => new(false, false, null);

    /// <summary>Registra a aprovação como Solution Partner.</summary>
    public void Approve(DateTimeOffset now)
    {
        if (IsApproved)
        {
            throw new DomainRuleException("A operação já está aprovada.");
        }

        IsApproved = true;
        ApprovedAt = now;
    }

    /// <summary>Registra a concessão da linha de crédito.</summary>
    public void GrantCreditLine()
    {
        if (!IsApproved)
        {
            throw new DomainRuleException(
                "A linha de crédito só pode ser concedida após a aprovação como Solution Partner.");
        }

        HasCreditLine = true;
    }

    /// <summary>Revoga a linha de crédito, suspendendo a operação.</summary>
    public void RevokeCreditLine() => HasCreditLine = false;

    /// <summary>
    /// Verifica se a operação pode enviar mensagens.
    /// Lança <see cref="DomainRuleException"/> quando falta um pré-requisito.
    /// </summary>
    public void EnsureCanOperate()
    {
        if (!IsApproved)
        {
            throw new DomainRuleException(
                "A operação do WhatsApp exige aprovação como Solution Partner da Meta.");
        }

        if (!HasCreditLine)
        {
            throw new DomainRuleException(
                "A operação do WhatsApp exige linha de crédito ativa junto à Meta.");
        }
    }
}
