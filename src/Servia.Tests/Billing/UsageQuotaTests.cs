using Servia.Domain.Billing;
using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Tests.Billing;

/// <summary>
/// F4.12 — UsageQuota por tenant e período.
/// F4.15 — franquia mensal de IA.
/// F4.16 — compra antecipada de créditos.
/// F4.17 — cobrança automática do consumo.
/// </summary>
public class UsageQuotaTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateOnly PeriodStart = new(2026, 11, 1);
    private static readonly DateOnly PeriodEnd = new(2026, 11, 30);
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private static UsageQuota NewQuota(int included = 100, QuotaResource resource = QuotaResource.AiUnits) =>
        UsageQuota.Create(TenantId, resource, PeriodStart, PeriodEnd, included);

    // ---------- F4.12: cota ----------

    [Fact]
    public void Criar_Cota_Valida()
    {
        var quota = NewQuota(100);

        Assert.Equal(100, quota.Included);
        Assert.Equal(0, quota.Consumed);
        Assert.Equal(100, quota.Remaining);
        Assert.Equal(TenantId, quota.TenantId);
    }

    [Fact]
    public void Criar_Cota_Com_Periodo_Invalido_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            UsageQuota.Create(TenantId, QuotaResource.AiUnits, PeriodEnd, PeriodStart, 100));
    }

    [Fact]
    public void Criar_Cota_Com_Franquia_Negativa_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            UsageQuota.Create(TenantId, QuotaResource.AiUnits, PeriodStart, PeriodEnd, -1));
    }

    [Fact]
    public void Consumir_Dentro_Da_Franquia()
    {
        var quota = NewQuota(100);

        Assert.True(quota.TryConsume(30));
        Assert.Equal(30, quota.Consumed);
        Assert.Equal(70, quota.Remaining);
        Assert.False(quota.IsOverIncluded);
    }

    [Fact]
    public void Consumir_Alem_Da_Franquia_Eh_Bloqueado()
    {
        var quota = NewQuota(100);

        Assert.True(quota.TryConsume(100));
        Assert.False(quota.TryConsume(1));
        Assert.Equal(100, quota.Consumed);
    }

    [Fact]
    public void Consumir_Quantidade_Invalida_Lanca_Excecao()
    {
        var quota = NewQuota();

        Assert.Throws<DomainRuleException>(() => quota.TryConsume(0));
    }

    [Fact]
    public void Teto_Do_Estabelecimento_Limita_Consumo()
    {
        var quota = NewQuota(100);
        quota.SetTenantLimit(50);

        Assert.True(quota.TryConsume(50));
        Assert.False(quota.TryConsume(1));
    }

    [Fact]
    public void Teto_Negativo_Lanca_Excecao()
    {
        var quota = NewQuota();

        Assert.Throws<DomainRuleException>(() => quota.SetTenantLimit(-1));
    }

    // ---------- F4.15: franquia de IA ----------

    [Fact]
    public void Franquia_De_IA_Vem_Do_Plano()
    {
        var plan = Plan.Create("PREMIUM", "Premium", Money.From(299m), 15,
            SiteCustomizationLevel.Advanced, includedAiUnits: 1000);

        Assert.Equal(1000, BillingRules.AiAllowance(plan));
    }

    [Fact]
    public void WhatsApp_E_IA_Tem_Cotas_Independentes()
    {
        var whatsApp = NewQuota(50, QuotaResource.WhatsAppMessages);
        var ai = NewQuota(100, QuotaResource.AiUnits);

        whatsApp.TryConsume(50);
        ai.TryConsume(50);

        Assert.Equal(0, whatsApp.Remaining);
        Assert.Equal(50, ai.Remaining);
    }

    // ---------- F4.16: creditos antecipados ----------

    [Fact]
    public void Adicionar_Creditos_Aumenta_Disponivel()
    {
        var quota = NewQuota(100);

        quota.AddExtra(50);

        Assert.Equal(150, quota.Available);
        Assert.Equal(150, quota.Remaining);
    }

    [Fact]
    public void Consumir_Usa_Creditos_Apos_Franquia()
    {
        var quota = NewQuota(100);
        quota.AddExtra(50);

        Assert.True(quota.TryConsume(150));
        Assert.False(quota.TryConsume(1));
    }

    [Fact]
    public void Compra_De_Creditos_So_Aplica_Apos_Pagamento()
    {
        var quota = NewQuota(100);
        var purchase = CreditPurchase.Create(
            TenantId, QuotaResource.AiUnits, 50, Money.From(25m), Now);

        // Ainda não pago: não pode aplicar.
        Assert.Throws<DomainRuleException>(() => purchase.ApplyTo(quota));

        purchase.MarkPaid(Guid.NewGuid());
        purchase.ApplyTo(quota);

        Assert.Equal(150, quota.Available);
    }

    [Fact]
    public void Compra_De_Recurso_Diferente_Nao_Aplica()
    {
        var quota = NewQuota(100, QuotaResource.AiUnits);
        var purchase = CreditPurchase.Create(
            TenantId, QuotaResource.WhatsAppMessages, 50, Money.From(25m), Now);
        purchase.MarkPaid(Guid.NewGuid());

        Assert.Throws<DomainRuleException>(() => purchase.ApplyTo(quota));
    }

    // ---------- F4.17: cobranca automatica ----------

    [Fact]
    public void Consumo_Dentro_Da_Franquia_Nao_Gera_Cobranca()
    {
        var quota = NewQuota(100);
        quota.TryConsume(80);

        var overage = BillingRules.CalculateOverage(quota, Money.From(0.10m));

        Assert.Equal(0m, overage.Amount);
    }

    [Fact]
    public void Consumo_Acima_Da_Franquia_Gera_Cobranca_Do_Excedente()
    {
        var quota = NewQuota(100);
        quota.AddExtra(50);
        quota.TryConsume(130); // 30 acima da franquia

        var overage = BillingRules.CalculateOverage(quota, Money.From(0.10m));

        Assert.Equal(3m, overage.Amount); // 30 * 0.10
    }

    [Fact]
    public void Cobranca_Automatica_So_Quando_Excede_Franquia()
    {
        var quota = NewQuota(100);
        quota.TryConsume(50);

        Assert.False(BillingRules.ShouldBillConsumption(quota));

        // Sem creditos extras, o consumo acima da franquia e bloqueado.
        // Para exceder, o tenant precisa de creditos comprados.
        quota.AddExtra(50);
        Assert.True(quota.TryConsume(60)); // total 110, acima da franquia
        Assert.True(BillingRules.ShouldBillConsumption(quota));
    }

    [Fact]
    public void Modalidade_PrePaga_Nao_Gera_Cobranca_Automatica()
    {
        var quota = UsageQuota.Create(
            TenantId, QuotaResource.WhatsAppMessages, PeriodStart, PeriodEnd, 100,
            ConsumptionBillingMode.PrepaidCredits);

        quota.AddExtra(50);
        quota.TryConsume(120);

        Assert.False(BillingRules.ShouldBillConsumption(quota));
    }

    [Fact]
    public void Reiniciar_Cota_Zera_Consumo()
    {
        var quota = NewQuota(100);
        quota.TryConsume(80);

        quota.Reset(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31), 100);

        Assert.Equal(0, quota.Consumed);
        Assert.Equal(100, quota.Remaining);
    }
}
