using Servia.Domain.Common;
using Servia.Domain.Finance;

namespace Servia.Tests.Finance;

/// <summary>
/// F5.6 — regras de comissão.
/// F5.7 — extrato do profissional.
/// </summary>
public class CommissionTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid ProfessionalId = Guid.NewGuid();
    private static readonly Guid AppointmentId = Guid.NewGuid();
    private static readonly DateOnly Period = new(2026, 11, 1);
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private static CommissionEntry NewEntry(decimal amount, Guid? professionalId = null) =>
        CommissionEntry.Create(
            TenantId, professionalId ?? ProfessionalId, AppointmentId,
            Money.From(amount), Period, Now);

    // ---------- F5.6: regras ----------

    [Fact]
    public void Comissao_Percentual_Calcula_Sobre_O_Valor()
    {
        var rule = CommissionRule.CreatePercentage(TenantId, 40m);

        var commission = rule.CalculateFor(Money.From(100m));

        Assert.Equal(40m, commission.Amount);
    }

    [Fact]
    public void Comissao_Fixa_Ignora_O_Valor_Do_Servico()
    {
        var rule = CommissionRule.CreateFixed(TenantId, Money.From(25m));

        Assert.Equal(25m, rule.CalculateFor(Money.From(100m)).Amount);
        Assert.Equal(25m, rule.CalculateFor(Money.From(500m)).Amount);
    }

    [Fact]
    public void Comissao_Por_Meta_Usa_O_Percentual()
    {
        var rule = CommissionRule.CreateTarget(TenantId, 10m, Money.From(5000m));

        Assert.Equal(10m, rule.CalculateFor(Money.From(100m)).Amount);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Percentual_Fora_Do_Intervalo_Lanca_Excecao(decimal percentage)
    {
        Assert.Throws<DomainRuleException>(() =>
            CommissionRule.CreatePercentage(TenantId, percentage));
    }

    [Fact]
    public void Valor_Fixo_Zero_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            CommissionRule.CreateFixed(TenantId, Money.From(0m)));
    }

    [Fact]
    public void Meta_Zero_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            CommissionRule.CreateTarget(TenantId, 10m, Money.From(0m)));
    }

    // ---------- Precedencia ----------

    [Fact]
    public void Regra_Padrao_Tem_Especificidade_Zero()
    {
        Assert.Equal(0, CommissionRule.CreatePercentage(TenantId, 30m).Specificity);
    }

    [Fact]
    public void Regra_Por_Profissional_Tem_Especificidade_1()
    {
        var rule = CommissionRule.CreatePercentage(TenantId, 30m, professionalId: ProfessionalId);

        Assert.Equal(1, rule.Specificity);
    }

    [Fact]
    public void Regra_Por_Servico_Tem_Especificidade_2()
    {
        var rule = CommissionRule.CreatePercentage(
            TenantId, 30m, professionalId: ProfessionalId, serviceId: Guid.NewGuid());

        Assert.Equal(2, rule.Specificity);
    }

    // ---------- F5.7: extrato ----------

    [Fact]
    public void Comissao_Inicia_Pendente()
    {
        var entry = NewEntry(40m);

        Assert.Equal(CommissionStatus.Pending, entry.Status);
        Assert.False(entry.IsPaid);
    }

    [Fact]
    public void Aprovar_E_Pagar_Comissao()
    {
        var entry = NewEntry(40m);

        entry.Approve();
        Assert.Equal(CommissionStatus.Approved, entry.Status);

        entry.MarkPaid(Now);
        Assert.Equal(CommissionStatus.Paid, entry.Status);
        Assert.True(entry.IsPaid);
    }

    [Fact]
    public void Pagar_Comissao_Nao_Aprovada_Lanca_Excecao()
    {
        var entry = NewEntry(40m);

        Assert.Throws<DomainRuleException>(() => entry.MarkPaid(Now));
    }

    [Fact]
    public void Comissao_Paga_Nao_Pode_Ser_Cancelada()
    {
        var entry = NewEntry(40m);
        entry.Approve();
        entry.MarkPaid(Now);

        Assert.Throws<DomainRuleException>(() => entry.Cancel());
    }

    [Fact]
    public void Extrato_Soma_Por_Status()
    {
        var pending = NewEntry(40m);
        var approved = NewEntry(30m);
        approved.Approve();
        var paid = NewEntry(20m);
        paid.Approve();
        paid.MarkPaid(Now);

        var statement = CommissionStatement.Build(
            ProfessionalId, Period, Period.AddMonths(1).AddDays(-1),
            [pending, approved, paid]);

        Assert.Equal(40m, statement.TotalPending.Amount);
        Assert.Equal(30m, statement.TotalApproved.Amount);
        Assert.Equal(20m, statement.TotalPaid.Amount);
        Assert.Equal(70m, statement.TotalReceivable.Amount);
    }

    [Fact]
    public void Extrato_Ignora_Comissoes_De_Outro_Profissional()
    {
        var mine = NewEntry(40m);
        var other = NewEntry(100m, Guid.NewGuid());

        var statement = CommissionStatement.Build(
            ProfessionalId, Period, Period.AddMonths(1).AddDays(-1), [mine, other]);

        Assert.Equal(40m, statement.TotalPending.Amount);
    }

    [Fact]
    public void Extrato_Sem_Comissoes_Eh_Zero()
    {
        var statement = CommissionStatement.Build(
            ProfessionalId, Period, Period.AddMonths(1).AddDays(-1), []);

        Assert.Equal(0m, statement.TotalReceivable.Amount);
    }
}
