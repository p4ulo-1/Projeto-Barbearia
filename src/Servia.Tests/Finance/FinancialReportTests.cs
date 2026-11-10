using Servia.Domain.Common;
using Servia.Domain.Finance;

namespace Servia.Tests.Finance;

/// <summary>
/// F5.4 — relatórios financeiros.
/// F5.8 — conciliação financeira.
/// </summary>
public class FinancialReportTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateOnly PeriodStart = new(2026, 11, 1);
    private static readonly DateOnly PeriodEnd = new(2026, 11, 30);
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private static Transaction Income(decimal amount) =>
        Transaction.Create(TenantId, TransactionType.Income, Money.From(amount), "Receita", Now);

    private static Transaction Expense(decimal amount) =>
        Transaction.Create(TenantId, TransactionType.Expense, Money.From(amount), "Despesa", Now);

    // ---------- F5.4: relatorio ----------

    [Fact]
    public void Relatorio_Soma_Receitas_E_Despesas()
    {
        var report = FinancialReport.Build(
            PeriodStart, PeriodEnd, [Income(100m), Income(50m), Expense(30m)], appointmentCount: 2);

        Assert.Equal(150m, report.TotalIncome.Amount);
        Assert.Equal(30m, report.TotalExpense.Amount);
        Assert.Equal(120m, report.Balance.Amount);
    }

    [Fact]
    public void Relatorio_Calcula_Ticket_Medio()
    {
        var report = FinancialReport.Build(
            PeriodStart, PeriodEnd, [Income(300m)], appointmentCount: 3);

        Assert.Equal(100m, report.AverageTicket.Amount);
    }

    [Fact]
    public void Ticket_Medio_Sem_Atendimentos_Eh_Zero()
    {
        var report = FinancialReport.Build(PeriodStart, PeriodEnd, [Income(300m)], appointmentCount: 0);

        Assert.Equal(0m, report.AverageTicket.Amount);
    }

    [Fact]
    public void Relatorio_Sem_Lancamentos_Eh_Zero()
    {
        var report = FinancialReport.Build(PeriodStart, PeriodEnd, [], appointmentCount: 0);

        Assert.Equal(0m, report.TotalIncome.Amount);
        Assert.Equal(0m, report.TotalExpense.Amount);
        Assert.Equal(0m, report.Balance.Amount);
    }

    [Fact]
    public void Relatorio_Com_Periodo_Invalido_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            FinancialReport.Build(PeriodEnd, PeriodStart, [], 0));
    }

    [Fact]
    public void Relatorio_Com_Atendimentos_Negativos_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            FinancialReport.Build(PeriodStart, PeriodEnd, [], -1));
    }

    // ---------- F5.8: conciliacao ----------

    [Fact]
    public void Conciliacao_Consistente_Quando_Pagamento_Tem_Lancamento()
    {
        var payment = Payment.RegisterInPerson(
            TenantId, Guid.NewGuid(), Money.From(100m), PaymentMethod.Cash, Now);
        var transaction = Transaction.FromPayment(payment, "Atendimento");

        var result = ReconciliationResult.Reconcile([payment], [transaction]);

        Assert.True(result.IsConsistent);
        Assert.Equal(1, result.MatchedCount);
    }

    [Fact]
    public void Conciliacao_Detecta_Pagamento_Sem_Lancamento()
    {
        var payment = Payment.RegisterInPerson(
            TenantId, Guid.NewGuid(), Money.From(100m), PaymentMethod.Cash, Now);

        var result = ReconciliationResult.Reconcile([payment], []);

        Assert.False(result.IsConsistent);
        Assert.Single(result.Divergences);
        Assert.Equal(ReconciliationIssue.MissingTransaction, result.Divergences[0].Issue);
    }

    [Fact]
    public void Conciliacao_Detecta_Valor_Divergente()
    {
        var payment = Payment.RegisterInPerson(
            TenantId, Guid.NewGuid(), Money.From(100m), PaymentMethod.Cash, Now);

        // Lançamento com valor diferente do pagamento.
        var transaction = Transaction.Create(
            TenantId, TransactionType.Income, Money.From(80m), "Atendimento", Now,
            paymentId: payment.Id);

        var result = ReconciliationResult.Reconcile([payment], [transaction]);

        Assert.False(result.IsConsistent);
        Assert.Equal(ReconciliationIssue.AmountMismatch, result.Divergences[0].Issue);
        Assert.Equal(100m, result.Divergences[0].Expected.Amount);
        Assert.Equal(80m, result.Divergences[0].Found.Amount);
    }

    [Fact]
    public void Conciliacao_Ignora_Pagamento_Pendente()
    {
        var pending = Payment.RegisterOnline(
            TenantId, Guid.NewGuid(), Money.From(100m), PaymentMethod.Pix, "pay_1", Now);

        var result = ReconciliationResult.Reconcile([pending], []);

        Assert.True(result.IsConsistent);
        Assert.Equal(0, result.MatchedCount);
    }

    [Fact]
    public void Conciliacao_Considera_Valor_Liquido_Apos_Estorno()
    {
        var payment = Payment.RegisterInPerson(
            TenantId, Guid.NewGuid(), Money.From(100m), PaymentMethod.Cash, Now);
        payment.Refund(Money.From(30m));

        // Lançamento do valor líquido: 70.
        var transaction = Transaction.Create(
            TenantId, TransactionType.Income, Money.From(70m), "Atendimento", Now,
            paymentId: payment.Id);

        var result = ReconciliationResult.Reconcile([payment], [transaction]);

        Assert.True(result.IsConsistent);
    }
}
