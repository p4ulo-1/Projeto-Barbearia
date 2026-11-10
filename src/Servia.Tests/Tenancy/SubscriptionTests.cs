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

    // ---------- Ativacao ----------

    [Fact]
    public void Ativar_Apos_Trial_Converte_Em_Ativa()
    {
        var subscription = NewTrial();
        var activationDate = Today.AddDays(14);

        subscription.ActivateAfterTrial(activationDate, "sub_456");

        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.True(subscription.IsActive);
        Assert.Null(subscription.TrialEndsAt);
        Assert.Equal("sub_456", subscription.GatewaySubscriptionId);
        Assert.Equal(activationDate.AddMonths(1), subscription.CurrentPeriodEnd);
    }

    [Fact]
    public void Ativar_Assinatura_Ja_Ativa_Lanca_Excecao()
    {
        var subscription = NewTrial();
        subscription.ActivateAfterTrial(Today.AddDays(14));

        Assert.Throws<DomainRuleException>(() =>
            subscription.ActivateAfterTrial(Today.AddDays(45)));
    }

    // ---------- Renovacao ----------

    [Fact]
    public void Renovar_Estende_Periodo_Por_Um_Mes()
    {
        var subscription = NewTrial();
        subscription.ActivateAfterTrial(Today.AddDays(14));

        var renewalDate = Today.AddDays(44);
        subscription.Renew(renewalDate);

        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.Equal(renewalDate, subscription.CurrentPeriodStart);
        Assert.Equal(renewalDate.AddMonths(1), subscription.CurrentPeriodEnd);
    }

    [Fact]
    public void Renovar_Assinatura_Cancelada_Lanca_Excecao()
    {
        var subscription = NewTrial();
        subscription.Cancel();

        Assert.Throws<DomainRuleException>(() => subscription.Renew(Today.AddDays(30)));
    }

    // ---------- Inadimplencia ----------

    [Fact]
    public void Marcar_Inadimplente_Altera_Status()
    {
        var subscription = NewTrial();
        subscription.ActivateAfterTrial(Today.AddDays(14));

        subscription.MarkPastDue();

        Assert.Equal(SubscriptionStatus.PastDue, subscription.Status);
        Assert.False(subscription.IsActive);
    }

    [Fact]
    public void Assinatura_Cancelada_Nao_Pode_Ficar_Inadimplente()
    {
        var subscription = NewTrial();
        subscription.Cancel();

        Assert.Throws<DomainRuleException>(() => subscription.MarkPastDue());
    }

    // ---------- Cancelamento ----------

    [Fact]
    public void Cancelar_Altera_Status_Para_Cancelada()
    {
        var subscription = NewTrial();

        subscription.Cancel();

        Assert.Equal(SubscriptionStatus.Cancelled, subscription.Status);
    }

    [Fact]
    public void Cancelar_Duas_Vezes_Lanca_Excecao()
    {
        var subscription = NewTrial();
        subscription.Cancel();

        Assert.Throws<DomainRuleException>(() => subscription.Cancel());
    }

    // ---------- Expiracao do periodo ----------

    [Fact]
    public void Periodo_Expira_Apos_Data_De_Fim()
    {
        var subscription = NewTrial();
        subscription.ActivateAfterTrial(Today.AddDays(14));

        var periodEnd = subscription.CurrentPeriodEnd;

        Assert.False(subscription.IsPeriodExpired(periodEnd));
        Assert.True(subscription.IsPeriodExpired(periodEnd.AddDays(1)));
    }
}
