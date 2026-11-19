using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Tests.Tenancy;

/// <summary>
/// Ciclo de vida da assinatura: trial de 14 dias, ativação, renovação,
/// inadimplência e cancelamento.
/// </summary>
public class SubscriptionTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid PlanId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 11, 10);

    private static Subscription NewTrial() =>
        Subscription.StartTrial(TenantId, PlanId, Today, "cus_123");

    // ---------- Trial ----------

    [Fact]
    public void Iniciar_Trial_Cria_Assinatura_Com_14_Dias()
    {
        var subscription = NewTrial();

        Assert.Equal(SubscriptionStatus.Trialing, subscription.Status);
        Assert.True(subscription.IsOnTrial);
        Assert.Equal(Today, subscription.CurrentPeriodStart);
        Assert.Equal(Today.AddDays(14), subscription.TrialEndsAt);
        Assert.Equal("cus_123", subscription.GatewayCustomerId);
    }

    [Fact]
    public void Iniciar_Trial_Sem_Tenant_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            Subscription.StartTrial(Guid.Empty, PlanId, Today));
    }

    [Fact]
    public void Iniciar_Trial_Sem_Plano_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            Subscription.StartTrial(TenantId, Guid.Empty, Today));
    }

    [Fact]
    public void Trial_Nao_Expira_Antes_Do_Prazo()
    {
        var subscription = NewTrial();

        Assert.False(subscription.IsTrialExpired(Today));
        Assert.False(subscription.IsTrialExpired(Today.AddDays(13)));
    }

    [Fact]
    public void Trial_Expira_No_Prazo()
    {
        var subscription = NewTrial();

        Assert.True(subscription.IsTrialExpired(Today.AddDays(14)));
        Assert.True(subscription.IsTrialExpired(Today.AddDays(20)));
    }

    private static Plan NewPlan(int capacity = 3) =>
        Plan.Create("PLAN", "Plano", Money.From(99m), capacity, SiteCustomizationLevel.Basic, 100);

    private static Subscription Authorized(Plan plan)
    {
        var checkout = Subscription.StartCheckout(TenantId, plan, Guid.NewGuid(), Today);
        checkout.BindCheckout("sub_456");
        checkout.AwaitConfirmation();
        checkout.ConfirmAuthorization("sub_456");
        return checkout;
    }

    private static Subscription Paid(Plan plan)
    {
        var checkout = Authorized(plan);
        checkout.ConfirmFirstPayment("sub_456", "pay_1", plan, 0, Today, Today.AddMonths(1));
        return checkout;
    }

    [Fact]
    public void Checkout_Exige_Autorizacao_E_Pagamento_Da_Mesma_Assinatura_E_Valida_Capacidade()
    {
        var plan = NewPlan();
        var checkout = Subscription.StartCheckout(TenantId, plan, Guid.NewGuid(), Today);
        Assert.Equal(SubscriptionStatus.PendingCheckout, checkout.Status);
        Assert.Equal(plan.MonthlyPrice.Amount, checkout.ContractPriceAmount);
        Assert.Equal("BRL", checkout.ContractCurrency);
        Assert.Equal(1, checkout.BillingIntervalMonths);
        checkout.BindCheckout("sub_456");
        Assert.Throws<DomainRuleException>(() => checkout.BindCheckout("outra"));
        checkout.AwaitConfirmation();
        Assert.Throws<DomainRuleException>(() =>
            checkout.ConfirmFirstPayment("sub_456", "pay_1", plan, 0, Today, Today.AddMonths(1)));
        Assert.Throws<DomainRuleException>(() => checkout.ConfirmAuthorization("outra"));
        checkout.ConfirmAuthorization("sub_456");
        Assert.False(checkout.IsActive);
        Assert.Throws<DomainRuleException>(() =>
            checkout.ConfirmFirstPayment("sub_456", "pay_1", plan, 4, Today, Today.AddMonths(1)));
        Assert.Equal(SubscriptionStatus.AuthorizedAwaitingFirstPayment, checkout.Status);
        Assert.Null(checkout.FirstPaymentId);
        checkout.ConfirmFirstPayment("sub_456", "pay_1", plan, 3, Today, Today.AddMonths(1));
        Assert.True(checkout.IsActive);
        Assert.Equal("pay_1", checkout.FirstPaymentId);
        Assert.Throws<DomainRuleException>(() =>
            checkout.ConfirmFirstPayment("sub_456", "pay_2", plan, 3, Today, Today.AddMonths(1)));
    }

    [Fact]
    public void Falha_Encerra_A_Tentativa_Sem_Ativar_O_Plano()
    {
        var plan = NewPlan();
        var checkout = Authorized(plan);
        checkout.FailCheckout(Today);
        Assert.Equal(SubscriptionStatus.Cancelled, checkout.Status);
        Assert.Equal(Today, checkout.CheckoutFailedOn);
        Assert.Equal("sub_456", checkout.GatewaySubscriptionId);
        Assert.Null(checkout.FirstPaymentId);
        Assert.Throws<DomainRuleException>(() =>
            checkout.ConfirmFirstPayment("sub_456", "pay_1", plan, 0, Today, Today.AddMonths(1)));
    }

    [Fact]
    public void Inadimplencia_Preserva_O_Inicio_E_Regularizacao_Exige_Pagamento_Novo()
    {
        var trial = NewTrial();
        var now = new DateTimeOffset(Today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        Assert.Throws<DomainRuleException>(() => trial.MarkPastDue(now));
        var paid = Paid(NewPlan());
        var currentEnd = paid.CurrentPeriodEnd;
        paid.MarkPastDue(now);
        paid.MarkPastDue(now.AddHours(10));
        Assert.Equal(now, paid.PastDueSince);
        Assert.Equal(SubscriptionStatus.PastDue, paid.Status);
        Assert.Throws<DomainRuleException>(() => paid.ConfirmRegularization("sub_456", "pay_1", currentEnd, currentEnd.AddMonths(1)));
        paid.ConfirmRegularization("sub_456", "pay_2", currentEnd, currentEnd.AddMonths(1));
        Assert.True(paid.IsActive);
        Assert.Null(paid.PastDueSince);
    }

    [Fact]
    public void Trial_Cancela_Imediatamente_E_Pago_Encerra_Apenas_Ao_Fim_Do_Periodo_Confirmado()
    {
        var trial = NewTrial();
        trial.CancelTrial(Today);
        Assert.Equal(SubscriptionStatus.Cancelled, trial.Status);
        Assert.Equal(Today, trial.CancellationEffectiveOn);
        var paid = Paid(NewPlan());
        Assert.Throws<DomainRuleException>(() => paid.CancelTrial(Today));
        paid.RequestCancellation(Today);
        Assert.True(paid.IsActive);
        Assert.Null(paid.CancellationEffectiveOn);
        paid.ConfirmCancellation("sub_456");
        Assert.Equal(paid.CurrentPeriodEnd, paid.CancellationEffectiveOn);
        Assert.Throws<DomainRuleException>(() => paid.EndCancellation(paid.CurrentPeriodEnd.AddDays(-1)));
        paid.EndCancellation(paid.CurrentPeriodEnd);
        Assert.Equal(SubscriptionStatus.Cancelled, paid.Status);
    }

    [Fact]
    public void Downgrade_Solicitado_Valida_Capacidade_E_Preserva_O_Plano_Efetivo()
    {
        var unlimited = NewPlan(0);
        var target = NewPlan();
        var paid = Paid(unlimited);
        var userId = Guid.NewGuid();
        var now = new DateTimeOffset(Today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        Assert.Throws<DomainRuleException>(() => PlanChange.Request(paid, unlimited, target, 4, userId, now));
        var change = PlanChange.Request(paid, unlimited, target, 3, userId, now);
        Assert.Equal(PlanChangeStatus.Requested, change.Status);
        Assert.Equal(PlanChangeReason.Downgrade, change.Reason);
        Assert.Equal(paid.CurrentPeriodEnd, change.EffectiveOn);
        Assert.Equal(unlimited.Id, paid.PlanId);
    }
}
