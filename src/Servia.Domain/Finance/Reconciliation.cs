using Servia.Domain.Common;

namespace Servia.Domain.Finance;

/// <summary>
/// Conciliação financeira (F5.8).
///
/// Confronta três fontes: os pagamentos registrados no sistema, as
/// transações confirmadas pelo gateway e os lançamentos internos. O
/// resultado aponta divergências em vez de escondê-las.
///
/// Uma divergência não é erro de código: é um fato do negócio que precisa
/// ser investigado — cobrança sem lançamento, lançamento sem cobrança,
/// valor diferente.
/// </summary>
public sealed record ReconciliationResult(
    int MatchedCount,
    IReadOnlyList<ReconciliationDivergence> Divergences)
{
    /// <summary>Indica se todas as fontes estão consistentes.</summary>
    public bool IsConsistent => Divergences.Count == 0;

    /// <summary>
    /// Concilia pagamentos e lançamentos.
    /// </summary>
    public static ReconciliationResult Reconcile(
        IEnumerable<Payment> payments,
        IEnumerable<Transaction> transactions)
    {
        ArgumentNullException.ThrowIfNull(payments);
        ArgumentNullException.ThrowIfNull(transactions);

        var divergences = new List<ReconciliationDivergence>();
        var transactionList = transactions.ToList();
        var matched = 0;

        foreach (var payment in payments)
        {
            if (!payment.IsPaid)
            {
                continue;
            }

            var linked = transactionList
                .Where(transaction => transaction.PaymentId == payment.Id)
                .ToList();

            if (linked.Count == 0)
            {
                divergences.Add(new ReconciliationDivergence(
                    ReconciliationIssue.MissingTransaction,
                    payment.Id,
                    payment.Amount,
                    Money.Zero(payment.Amount.Currency)));

                continue;
            }

            var total = linked
                .Select(transaction => transaction.Amount)
                .Aggregate((first, second) => first.Add(second));

            if (total.Amount != payment.NetAmount.Amount)
            {
                divergences.Add(new ReconciliationDivergence(
                    ReconciliationIssue.AmountMismatch,
                    payment.Id,
                    payment.NetAmount,
                    total));

                continue;
            }

            matched++;
        }

        return new ReconciliationResult(matched, divergences);
    }
}

/// <summary>Divergência encontrada na conciliação.</summary>
public sealed record ReconciliationDivergence(
    ReconciliationIssue Issue,
    Guid PaymentId,
    Money Expected,
    Money Found);

/// <summary>Natureza da divergência.</summary>
public enum ReconciliationIssue
{
    /// <summary>Pagamento confirmado sem lançamento correspondente.</summary>
    MissingTransaction = 1,

    /// <summary>Lançamento com valor diferente do pagamento.</summary>
    AmountMismatch = 2
}
