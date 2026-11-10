using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Finance;

/// <summary>
/// Lançamento financeiro do estabelecimento (F5.3).
///
/// Registra receita ou despesa, com categoria e descrição. É a base dos
/// relatórios (F5.4) e da conciliação (F5.8).
///
/// Um lançamento pode estar vinculado a um pagamento: nesse caso, é a
/// contrapartida contábil do recebimento.
/// </summary>
public sealed class Transaction : ITenantScoped
{
    private Transaction(
        Guid id,
        Guid tenantId,
        TransactionType type,
        Guid? categoryId,
        Money amount,
        string description,
        DateTimeOffset occurredAt,
        Guid? paymentId)
    {
        Id = id;
        TenantId = tenantId;
        Type = type;
        CategoryId = categoryId;
        Amount = amount;
        Description = description;
        OccurredAt = occurredAt;
        PaymentId = paymentId;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>.
    /// </summary>
    private Transaction()
    {
        Description = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public TransactionType Type { get; private set; }

    public Guid? CategoryId { get; private set; }

    public Money Amount { get; private set; }

    public string Description { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Pagamento que originou o lançamento, quando houver.</summary>
    public Guid? PaymentId { get; private set; }

    /// <summary>Indica se o lançamento é uma entrada de caixa.</summary>
    public bool IsIncome => Type == TransactionType.Income;

    public static Transaction Create(
        Guid tenantId,
        TransactionType type,
        Money amount,
        string description,
        DateTimeOffset occurredAt,
        Guid? categoryId = null,
        Guid? paymentId = null)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (amount.Amount <= 0)
        {
            throw new DomainRuleException("O valor do lançamento deve ser maior que zero.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new DomainRuleException("A descrição do lançamento é obrigatória.");
        }

        return new Transaction(
            Guid.NewGuid(),
            tenantId,
            type,
            categoryId,
            amount,
            description.Trim(),
            occurredAt,
            paymentId);
    }

    /// <summary>Cria a receita correspondente a um pagamento confirmado.</summary>
    public static Transaction FromPayment(Payment payment, string description)
    {
        ArgumentNullException.ThrowIfNull(payment);

        if (!payment.IsPaid)
        {
            throw new DomainRuleException(
                "Somente pagamentos confirmados geram lançamento de receita.");
        }

        return Create(
            payment.TenantId,
            TransactionType.Income,
            payment.Amount,
            description,
            payment.PaidAt ?? payment.CreatedAt,
            paymentId: payment.Id);
    }

    /// <summary>Altera a categoria do lançamento.</summary>
    public void ChangeCategory(Guid? categoryId) => CategoryId = categoryId;

    /// <summary>Atualiza a descrição do lançamento.</summary>
    public void UpdateDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new DomainRuleException("A descrição do lançamento é obrigatória.");
        }

        Description = description.Trim();
    }
}

/// <summary>Tipo do lançamento financeiro.</summary>
public enum TransactionType
{
    /// <summary>Entrada de caixa.</summary>
    Income = 1,

    /// <summary>Saída de caixa.</summary>
    Expense = 2
}
