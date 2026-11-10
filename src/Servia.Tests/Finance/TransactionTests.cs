using Servia.Domain.Common;
using Servia.Domain.Finance;

namespace Servia.Tests.Finance;

/// <summary>
/// F5.3 — receitas e despesas com categorias.
/// </summary>
public class TransactionTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Registrar_Receita()
    {
        var transaction = Transaction.Create(
            TenantId, TransactionType.Income, Money.From(150m), "Corte + barba", Now);

        Assert.True(transaction.IsIncome);
        Assert.Equal(150m, transaction.Amount.Amount);
        Assert.Equal("Corte + barba", transaction.Description);
    }

    [Fact]
    public void Registrar_Despesa()
    {
        var transaction = Transaction.Create(
            TenantId, TransactionType.Expense, Money.From(80m), "Compra de produtos", Now);

        Assert.False(transaction.IsIncome);
        Assert.Equal(80m, transaction.Amount.Amount);
    }

    [Fact]
    public void Registrar_Lancamento_Com_Categoria()
    {
        var categoryId = Guid.NewGuid();

        var transaction = Transaction.Create(
            TenantId, TransactionType.Expense, Money.From(80m), "Produtos", Now, categoryId);

        Assert.Equal(categoryId, transaction.CategoryId);
    }

    [Fact]
    public void Registrar_Lancamento_Sem_Tenant_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            Transaction.Create(Guid.Empty, TransactionType.Income, Money.From(100m), "X", Now));
    }

    [Fact]
    public void Registrar_Lancamento_Com_Valor_Zero_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            Transaction.Create(TenantId, TransactionType.Income, Money.From(0m), "X", Now));
    }

    [Fact]
    public void Registrar_Lancamento_Sem_Descricao_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            Transaction.Create(TenantId, TransactionType.Income, Money.From(100m), "  ", Now));
    }

    // ---------- Vinculo com pagamento ----------

    [Fact]
    public void Pagamento_Confirmado_Gera_Receita()
    {
        var payment = Payment.RegisterInPerson(
            TenantId, Guid.NewGuid(), Money.From(150m), PaymentMethod.Cash, Now);

        var transaction = Transaction.FromPayment(payment, "Atendimento");

        Assert.True(transaction.IsIncome);
        Assert.Equal(150m, transaction.Amount.Amount);
        Assert.Equal(payment.Id, transaction.PaymentId);
    }

    [Fact]
    public void Pagamento_Pendente_Nao_Gera_Receita()
    {
        var payment = Payment.RegisterOnline(
            TenantId, Guid.NewGuid(), Money.From(150m), PaymentMethod.Pix, "pay_1", Now);

        Assert.Throws<DomainRuleException>(() =>
            Transaction.FromPayment(payment, "Atendimento"));
    }

    // ---------- Alteracoes ----------

    [Fact]
    public void Alterar_Categoria_Do_Lancamento()
    {
        var transaction = Transaction.Create(
            TenantId, TransactionType.Expense, Money.From(80m), "Produtos", Now);
        var categoryId = Guid.NewGuid();

        transaction.ChangeCategory(categoryId);

        Assert.Equal(categoryId, transaction.CategoryId);
    }

    [Fact]
    public void Atualizar_Descricao_Sem_Texto_Lanca_Excecao()
    {
        var transaction = Transaction.Create(
            TenantId, TransactionType.Income, Money.From(100m), "X", Now);

        Assert.Throws<DomainRuleException>(() => transaction.UpdateDescription("  "));
    }
}
