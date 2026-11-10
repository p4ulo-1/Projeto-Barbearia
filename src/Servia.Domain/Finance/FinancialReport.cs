using Servia.Domain.Common;

namespace Servia.Domain.Finance;

/// <summary>
/// Relatório financeiro do estabelecimento (F5.4).
///
/// Calculado a partir dos lançamentos do período. É um objeto de leitura:
/// não altera estado, apenas resume o que existe.
///
/// O ticket médio é calculado sobre as receitas vinculadas a atendimentos,
/// não sobre todas as entradas — um aporte de capital não infla o indicador.
/// </summary>
public sealed record FinancialReport(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    Money TotalIncome,
    Money TotalExpense,
    int AppointmentCount)
{
    /// <summary>Resultado do período: receitas menos despesas.</summary>
    public Money Balance => TotalIncome.Subtract(TotalExpense);

    /// <summary>
    /// Ticket médio por atendimento.
    /// Zero quando não há atendimentos no período.
    /// </summary>
    public Money AverageTicket =>
        AppointmentCount == 0
            ? Money.Zero(TotalIncome.Currency)
            : TotalIncome.DivideBy(AppointmentCount);

    /// <summary>
    /// Calcula o relatório a partir dos lançamentos do período.
    /// </summary>
    public static FinancialReport Build(
        DateOnly periodStart,
        DateOnly periodEnd,
        IEnumerable<Transaction> transactions,
        int appointmentCount)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        if (periodStart > periodEnd)
        {
            throw new DomainRuleException("O início do período deve ser anterior ao fim.");
        }

        if (appointmentCount < 0)
        {
            throw new DomainRuleException("A quantidade de atendimentos não pode ser negativa.");
        }

        var list = transactions.ToList();
        var currency = list.Count > 0 ? list[0].Amount.Currency : Money.DefaultCurrency;

        var income = Money.Zero(currency);
        var expense = Money.Zero(currency);

        foreach (var transaction in list)
        {
            if (transaction.IsIncome)
            {
                income = income.Add(transaction.Amount);
            }
            else
            {
                expense = expense.Add(transaction.Amount);
            }
        }

        return new FinancialReport(periodStart, periodEnd, income, expense, appointmentCount);
    }
}

/// <summary>
/// Extrato de comissões de um profissional (F5.7).
/// </summary>
public sealed record CommissionStatement(
    Guid ProfessionalId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    Money TotalPending,
    Money TotalApproved,
    Money TotalPaid)
{
    /// <summary>Total a receber: pendente mais aprovado.</summary>
    public Money TotalReceivable => TotalPending.Add(TotalApproved);

    /// <summary>
    /// Calcula o extrato a partir das entradas do profissional no período.
    /// </summary>
    public static CommissionStatement Build(
        Guid professionalId,
        DateOnly periodStart,
        DateOnly periodEnd,
        IEnumerable<CommissionEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (professionalId == Guid.Empty)
        {
            throw new DomainRuleException("O profissional é obrigatório.");
        }

        if (periodStart > periodEnd)
        {
            throw new DomainRuleException("O início do período deve ser anterior ao fim.");
        }

        var list = entries.ToList();
        var currency = list.Count > 0 ? list[0].Amount.Currency : Money.DefaultCurrency;

        var pending = Money.Zero(currency);
        var approved = Money.Zero(currency);
        var paid = Money.Zero(currency);

        foreach (var entry in list)
        {
            if (entry.ProfessionalId != professionalId)
            {
                continue;
            }

            switch (entry.Status)
            {
                case CommissionStatus.Pending:
                    pending = pending.Add(entry.Amount);
                    break;
                case CommissionStatus.Approved:
                    approved = approved.Add(entry.Amount);
                    break;
                case CommissionStatus.Paid:
                    paid = paid.Add(entry.Amount);
                    break;
            }
        }

        return new CommissionStatement(professionalId, periodStart, periodEnd, pending, approved, paid);
    }
}
