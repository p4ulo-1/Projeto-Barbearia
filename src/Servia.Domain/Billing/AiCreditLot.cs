using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Billing;

/// <summary>
/// Lote de créditos adicionais de IA (gestão de IA, §7.1).
///
/// <b>Por que um lote e não um saldo único:</b> cada aquisição tem sua própria
/// validade. Comprar um pacote novo <b>não</b> renova a validade dos lotes
/// anteriores, e créditos expirados não podem ser consumidos. Um saldo
/// agregado não conseguiria representar isso.
///
/// <b>Separação de responsabilidades:</b> <see cref="CreditPurchase"/> é o
/// registro comercial da compra (valor pago, fatura, status). O lote é o
/// controle de saldo. Uma compra paga gera um lote.
///
/// <b>Ordem de consumo (§5.4):</b> a franquia mensal do plano é consumida
/// antes dos lotes. Entre lotes, consome-se primeiro o que expira antes.
/// </summary>
public sealed class AiCreditLot : ITenantScoped
{
    private AiCreditLot(
        Guid id,
        Guid tenantId,
        int quantity,
        Money amount,
        decimal discountPercent,
        DateTimeOffset acquiredAt,
        DateTimeOffset expiresAt)
    {
        Id = id;
        TenantId = tenantId;
        Quantity = quantity;
        Amount = amount;
        DiscountPercent = discountPercent;
        AcquiredAt = acquiredAt;
        ExpiresAt = expiresAt;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>.
    /// </summary>
    private AiCreditLot()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>Quantidade adquirida no lote.</summary>
    public int Quantity { get; private set; }

    /// <summary>Quantidade já consumida do lote.</summary>
    public int Consumed { get; private set; }

    /// <summary>Valor pago pelo lote.</summary>
    public Money Amount { get; private set; }

    /// <summary>Desconto aplicado sobre o preço-base, em percentual.</summary>
    public decimal DiscountPercent { get; private set; }

    public DateTimeOffset AcquiredAt { get; private set; }

    /// <summary>
    /// Data de expiração do lote. Créditos expirados não podem ser consumidos.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Saldo disponível do lote.</summary>
    public int Remaining => Math.Max(0, Quantity - Consumed);

    /// <summary>Indica se o lote ainda tem créditos.</summary>
    public bool HasBalance => Remaining > 0;

    /// <summary>
    /// Indica se o lote expirou na data informada.
    /// </summary>
    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;

    /// <summary>
    /// Indica se o lote pode ser consumido: tem saldo e não expirou.
    /// </summary>
    public bool IsConsumable(DateTimeOffset now) => HasBalance && !IsExpired(now);

    public static AiCreditLot Create(
        Guid tenantId,
        int quantity,
        Money amount,
        decimal discountPercent,
        DateTimeOffset acquiredAt,
        DateTimeOffset expiresAt)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (quantity <= 0)
        {
            throw new DomainRuleException("A quantidade de créditos deve ser maior que zero.");
        }

        if (discountPercent is < 0 or > 100)
        {
            throw new DomainRuleException("O desconto deve estar entre 0 e 100 por cento.");
        }

        if (expiresAt <= acquiredAt)
        {
            throw new DomainRuleException("A expiração deve ser posterior à aquisição.");
        }

        return new AiCreditLot(
            Guid.NewGuid(), tenantId, quantity, amount, discountPercent, acquiredAt, expiresAt);
    }

    /// <summary>
    /// Consome créditos do lote. Retorna quantos foram efetivamente consumidos,
    /// que pode ser menor que o solicitado quando o saldo é insuficiente.
    /// </summary>
    public int Consume(int quantity, DateTimeOffset now)
    {
        if (quantity <= 0)
        {
            throw new DomainRuleException("A quantidade consumida deve ser maior que zero.");
        }

        if (IsExpired(now))
        {
            throw new DomainRuleException("Créditos expirados não podem ser consumidos.");
        }

        var consumed = Math.Min(quantity, Remaining);
        Consumed += consumed;

        return consumed;
    }
}
