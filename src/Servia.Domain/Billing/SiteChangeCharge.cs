using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Billing;

/// <summary>
/// Cobrança avulsa de uma alteração de site solicitada após a publicação.
///
/// A geração inicial e a publicação estão incluídas no plano. Apenas
/// alterações posteriores ao lançamento geram cobrança, registrada
/// <b>antes</b> de executar a alteração — o tenant sabe o custo antes de
/// autorizar (ADR-007).
/// </summary>
public sealed class SiteChangeCharge : ITenantScoped
{
    private SiteChangeCharge(
        Guid id,
        Guid tenantId,
        Guid siteId,
        Guid requestId,
        Money amount)
    {
        Id = id;
        TenantId = tenantId;
        SiteId = siteId;
        RequestId = requestId;
        Amount = amount;
        Status = SiteChangeChargeStatus.Pending;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>.
    /// </summary>
    private SiteChangeCharge()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid SiteId { get; private set; }

    /// <summary>Solicitação de alteração que originou a cobrança.</summary>
    public Guid RequestId { get; private set; }

    public Money Amount { get; private set; }

    public SiteChangeChargeStatus Status { get; private set; }

    public Guid? PlatformInvoiceId { get; private set; }

    public static SiteChangeCharge Create(
        Guid tenantId,
        Guid siteId,
        Guid requestId,
        Money amount)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (siteId == Guid.Empty)
        {
            throw new DomainRuleException("O site é obrigatório.");
        }

        if (requestId == Guid.Empty)
        {
            throw new DomainRuleException("A solicitação de alteração é obrigatória.");
        }

        return new SiteChangeCharge(Guid.NewGuid(), tenantId, siteId, requestId, amount);
    }

    /// <summary>Vincula a cobrança à fatura em que será cobrada.</summary>
    public void MarkBilled(Guid platformInvoiceId)
    {
        if (Status == SiteChangeChargeStatus.Billed)
        {
            throw new DomainRuleException("A cobrança já foi faturada.");
        }

        if (platformInvoiceId == Guid.Empty)
        {
            throw new DomainRuleException("A fatura é obrigatória.");
        }

        Status = SiteChangeChargeStatus.Billed;
        PlatformInvoiceId = platformInvoiceId;
    }

    /// <summary>Isenta a cobrança, com motivo registrado.</summary>
    public void Waive(string reason)
    {
        if (Status == SiteChangeChargeStatus.Billed)
        {
            throw new DomainRuleException("Uma cobrança faturada não pode ser isentada.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainRuleException("O motivo da isenção é obrigatório.");
        }

        Status = SiteChangeChargeStatus.Waived;
    }
}

/// <summary>Estado da cobrança de alteração de site.</summary>
public enum SiteChangeChargeStatus
{
    Pending = 1,
    Billed = 2,
    Waived = 3
}
