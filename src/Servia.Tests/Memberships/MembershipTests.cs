using Servia.Domain.Common;
using Servia.Domain.Memberships;

namespace Servia.Tests.Memberships;

/// <summary>
/// F7.1 — clubes de assinatura.
/// F7.3 — controle de benefícios.
/// F7.4 — gestão de inadimplência do clube.
/// </summary>
public class MembershipTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid ServiceId = Guid.NewGuid();
    private static readonly DateOnly PeriodStart = new(2026, 11, 1);
    private static readonly DateOnly PeriodEnd = new(2026, 11, 30);

    private static MembershipPlan NewPlan()
    {
        var plan = MembershipPlan.Create(TenantId, "Clube do Corte", Money.From(99m));
        plan.AddServiceBenefit(ServiceId, 4);
        return plan;
    }

    private static Membership NewMembership() =>
        Membership.Subscribe(TenantId, CustomerId, Guid.NewGuid(), PeriodStart, PeriodEnd);

    // ---------- F7.1: clube ----------

    [Fact]
    public void Criar_Clube_Com_Beneficios()
    {
        var plan = NewPlan();

        Assert.Equal("Clube do Corte", plan.Name);
        Assert.Equal(99m, plan.MonthlyPrice.Amount);
        Assert.Single(plan.Benefits);
        Assert.True(plan.IsActive);
    }

    [Fact]
    public void Criar_Clube_Sem_Nome_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            MembershipPlan.Create(TenantId, "  ", Money.From(99m)));
    }

    [Fact]
    public void Criar_Clube_Com_Mensalidade_Zero_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            MembershipPlan.Create(TenantId, "Clube", Money.From(0m)));
    }

    [Fact]
    public void Adicionar_Beneficio_De_Desconto()
    {
        var plan = MembershipPlan.Create(TenantId, "Clube", Money.From(99m));

        var benefit = plan.AddDiscountBenefit(15m);

        Assert.True(benefit.IsDiscount);
        Assert.Equal(15m, benefit.DiscountPercentage);
    }

    [Fact]
    public void Desconto_Fora_Do_Intervalo_Lanca_Excecao()
    {
        var plan = MembershipPlan.Create(TenantId, "Clube", Money.From(99m));

        Assert.Throws<DomainRuleException>(() => plan.AddDiscountBenefit(0m));
        Assert.Throws<DomainRuleException>(() => plan.AddDiscountBenefit(101m));
    }

    // ---------- F7.3: controle de beneficios ----------

    [Fact]
    public void Assinar_Clube_Inicia_Ativo()
    {
        var membership = NewMembership();

        Assert.True(membership.IsActive);
        Assert.Equal(CustomerId, membership.CustomerId);
    }

    [Fact]
    public void Consumir_Beneficio_Reduz_Saldo()
    {
        var plan = NewPlan();
        var membership = NewMembership();

        membership.ConsumeBenefit(ServiceId, 1, plan);

        Assert.Equal(3, membership.RemainingFor(ServiceId, plan));
    }

    [Fact]
    public void Consumir_Alem_Do_Saldo_Lanca_Excecao()
    {
        var plan = NewPlan();
        var membership = NewMembership();
        membership.ConsumeBenefit(ServiceId, 4, plan);

        Assert.Throws<DomainRuleException>(() =>
            membership.ConsumeBenefit(ServiceId, 1, plan));
    }

    [Fact]
    public void Consumir_Servico_Fora_Do_Clube_Lanca_Excecao()
    {
        var plan = NewPlan();
        var membership = NewMembership();

        Assert.Throws<DomainRuleException>(() =>
            membership.ConsumeBenefit(Guid.NewGuid(), 1, plan));
    }

    [Fact]
    public void Renovar_Zera_O_Consumo()
    {
        var plan = NewPlan();
        var membership = NewMembership();
        membership.ConsumeBenefit(ServiceId, 3, plan);

        membership.Renew(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31));

        Assert.Equal(4, membership.RemainingFor(ServiceId, plan));
    }

    // ---------- F7.4: inadimplencia ----------

    [Fact]
    public void Suspender_Impede_Consumo()
    {
        var plan = NewPlan();
        var membership = NewMembership();

        membership.Suspend();

        Assert.True(membership.IsSuspended);
        Assert.Throws<DomainRuleException>(() =>
            membership.ConsumeBenefit(ServiceId, 1, plan));
    }

    [Fact]
    public void Reativar_Permite_Consumo_Novamente()
    {
        var plan = NewPlan();
        var membership = NewMembership();
        membership.Suspend();

        membership.Reactivate();
        membership.ConsumeBenefit(ServiceId, 1, plan);

        Assert.True(membership.IsActive);
        Assert.Equal(3, membership.RemainingFor(ServiceId, plan));
    }

    [Fact]
    public void Suspensao_Preserva_O_Historico_De_Consumo()
    {
        var plan = NewPlan();
        var membership = NewMembership();
        membership.ConsumeBenefit(ServiceId, 2, plan);

        membership.Suspend();

        Assert.Equal(2, membership.ConsumedByService[ServiceId]);
    }

    [Fact]
    public void Cancelar_Torna_Assinatura_Terminal()
    {
        var membership = NewMembership();

        membership.Cancel();

        Assert.Throws<DomainRuleException>(() => membership.Reactivate());
        Assert.Throws<DomainRuleException>(() =>
            membership.Renew(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public void Periodo_Expirado_Eh_Detectado()
    {
        var membership = NewMembership();

        Assert.False(membership.IsPeriodExpired(PeriodEnd));
        Assert.True(membership.IsPeriodExpired(PeriodEnd.AddDays(1)));
    }
}
