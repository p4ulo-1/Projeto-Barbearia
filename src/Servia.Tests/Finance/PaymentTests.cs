using Servia.Domain.Common;
using Servia.Domain.Finance;

namespace Servia.Tests.Finance;

/// <summary>
/// F5.1 — registro de pagamentos presenciais.
/// F5.2 — pagamento online confirmado por webhook.
/// F5.5 — estornos e ajustes.
/// </summary>
public class PaymentTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid AppointmentId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private static Payment InPerson(decimal amount = 100m) =>
        Payment.RegisterInPerson(TenantId, AppointmentId, Money.From(amount), PaymentMethod.Cash, Now);

    private static Payment Online(decimal amount = 100m) =>
        Payment.RegisterOnline(TenantId, AppointmentId, Money.From(amount),
            PaymentMethod.Pix, "pay_123", Now);

    // ---------- F5.1: pagamento presencial ----------

    [Fact]
    public void Registrar_Pagamento_Presencial_Ja_Confirmado()
    {
        var payment = InPerson();

        Assert.Equal(PaymentStatus.Paid, payment.Status);
        Assert.True(payment.IsPaid);
        Assert.Equal(Now, payment.PaidAt);
        Assert.Equal(AppointmentId, payment.AppointmentId);
        Assert.Equal(PaymentMethod.Cash, payment.Method);
    }

    [Fact]
    public void Registrar_Pagamento_Sem_Tenant_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            Payment.RegisterInPerson(Guid.Empty, AppointmentId, Money.From(100m), PaymentMethod.Cash, Now));
    }

    [Fact]
    public void Registrar_Pagamento_Com_Valor_Zero_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            Payment.RegisterInPerson(TenantId, AppointmentId, Money.From(0m), PaymentMethod.Cash, Now));
    }

    [Fact]
    public void Pagamento_Pode_Existir_Sem_Atendimento()
    {
        var payment = Payment.RegisterInPerson(
            TenantId, null, Money.From(50m), PaymentMethod.Cash, Now);

        Assert.Null(payment.AppointmentId);
    }

    // ---------- F5.2: pagamento online ----------

    [Fact]
    public void Registrar_Pagamento_Online_Inicia_Pendente()
    {
        var payment = Online();

        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.False(payment.IsPaid);
        Assert.Equal("pay_123", payment.GatewayPaymentId);
    }

    [Fact]
    public void Registrar_Pagamento_Online_Sem_Id_Do_Gateway_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            Payment.RegisterOnline(TenantId, AppointmentId, Money.From(100m),
                PaymentMethod.Pix, "  ", Now));
    }

    [Fact]
    public void Confirmar_Pagamento_Online_Pelo_Webhook()
    {
        var payment = Online();

        payment.Confirm(Now.AddMinutes(5));

        Assert.Equal(PaymentStatus.Paid, payment.Status);
        Assert.Equal(Now.AddMinutes(5), payment.PaidAt);
    }

    [Fact]
    public void Confirmar_Pagamento_Ja_Confirmado_Lanca_Excecao()
    {
        var payment = Online();
        payment.Confirm(Now);

        Assert.Throws<DomainRuleException>(() => payment.Confirm(Now));
    }

    [Fact]
    public void Marcar_Pagamento_Online_Como_Falho()
    {
        var payment = Online();

        payment.Fail();

        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.False(payment.IsPaid);
    }

    [Fact]
    public void Pagamento_Ja_Confirmado_Nao_Pode_Falhar()
    {
        var payment = InPerson();

        Assert.Throws<DomainRuleException>(() => payment.Fail());
    }

    // ---------- F5.5: estornos ----------

    [Fact]
    public void Estorno_Total_Muda_Status_Para_Estornado()
    {
        var payment = InPerson(100m);

        payment.Refund(Money.From(100m));

        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.Equal(0m, payment.NetAmount.Amount);
    }

    [Fact]
    public void Estorno_Parcial_Muda_Status_Para_Parcialmente_Estornado()
    {
        var payment = InPerson(100m);

        payment.Refund(Money.From(30m));

        Assert.Equal(PaymentStatus.PartiallyRefunded, payment.Status);
        Assert.Equal(30m, payment.RefundedAmount.Amount);
        Assert.Equal(70m, payment.NetAmount.Amount);
    }

    [Fact]
    public void Estornos_Sucessivos_Somam()
    {
        var payment = InPerson(100m);

        payment.Refund(Money.From(30m));
        payment.Refund(Money.From(70m));

        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.Equal(100m, payment.RefundedAmount.Amount);
    }

    [Fact]
    public void Estorno_Acima_Do_Valor_Pago_Lanca_Excecao()
    {
        var payment = InPerson(100m);

        Assert.Throws<DomainRuleException>(() => payment.Refund(Money.From(150m)));
    }

    [Fact]
    public void Estorno_De_Pagamento_Pendente_Lanca_Excecao()
    {
        var payment = Online();

        Assert.Throws<DomainRuleException>(() => payment.Refund(Money.From(10m)));
    }

    [Fact]
    public void Estorno_Com_Valor_Zero_Lanca_Excecao()
    {
        var payment = InPerson(100m);

        Assert.Throws<DomainRuleException>(() => payment.Refund(Money.From(0m)));
    }
}
