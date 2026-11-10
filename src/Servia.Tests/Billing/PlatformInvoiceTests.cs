using Servia.Domain.Billing;
using Servia.Domain.Common;

namespace Servia.Tests.Billing;

/// <summary>
/// F4.3 — PlatformInvoice e ciclo de faturamento.
/// F4.14 — linhas de cobrança separadas.
/// </summary>
public class PlatformInvoiceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid SubscriptionId = Guid.NewGuid();
    private static readonly DateOnly PeriodStart = new(2026, 11, 1);
    private static readonly DateOnly PeriodEnd = new(2026, 11, 30);
    private static readonly DateOnly DueDate = new(2026, 12, 5);
    private static readonly DateTimeOffset Now = new(2026, 12, 1, 12, 0, 0, TimeSpan.Zero);

    private static PlatformInvoice NewInvoice() =>
        PlatformInvoice.Create(TenantId, SubscriptionId, PeriodStart, PeriodEnd, DueDate);

    // ---------- F4.3: criacao ----------

    [Fact]
    public void Criar_Fatura_Valida_Inicia_Pendente()
    {
        var invoice = NewInvoice();

        Assert.Equal(InvoiceStatus.Pending, invoice.Status);
        Assert.False(invoice.IsPaid);
        Assert.Equal(TenantId, invoice.TenantId);
        Assert.Empty(invoice.Lines);
    }

    [Fact]
    public void Criar_Fatura_Sem_Tenant_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            PlatformInvoice.Create(Guid.Empty, SubscriptionId, PeriodStart, PeriodEnd, DueDate));
    }

    [Fact]
    public void Criar_Fatura_Com_Periodo_Invalido_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            PlatformInvoice.Create(TenantId, SubscriptionId, PeriodEnd, PeriodStart, DueDate));
    }

    [Fact]
    public void Criar_Fatura_Com_Vencimento_Antes_Do_Periodo_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            PlatformInvoice.Create(TenantId, SubscriptionId, PeriodStart, PeriodEnd, PeriodStart));
    }

    // ---------- F4.14: linhas separadas ----------

    [Fact]
    public void Adicionar_Linha_De_Assinatura()
    {
        var invoice = NewInvoice();

        invoice.AddLine(InvoiceLineType.Subscription, "Plano Essencial", Money.From(99m));

        Assert.Single(invoice.Lines);
        Assert.Equal(99m, invoice.Total.Amount);
    }

    [Fact]
    public void WhatsApp_E_IA_Sao_Linhas_Separadas()
    {
        var invoice = NewInvoice();

        invoice.AddLine(InvoiceLineType.Subscription, "Plano Essencial", Money.From(99m));
        invoice.AddLine(InvoiceLineType.WhatsApp, "Consumo de WhatsApp", Money.From(25m));
        invoice.AddLine(InvoiceLineType.Ai, "Consumo de IA", Money.From(15m));

        Assert.Equal(3, invoice.Lines.Count);
        Assert.Equal(139m, invoice.Total.Amount);

        // Cada recurso tem sua própria linha: um não encobre o outro.
        Assert.Single(invoice.Lines, line => line.Type == InvoiceLineType.WhatsApp);
        Assert.Single(invoice.Lines, line => line.Type == InvoiceLineType.Ai);
    }

    [Fact]
    public void Total_Eh_Sempre_A_Soma_Das_Linhas()
    {
        var invoice = NewInvoice();

        invoice.AddLine(InvoiceLineType.Subscription, "Plano", Money.From(99m));
        invoice.AddLine(InvoiceLineType.SiteChange, "Alteração de site", Money.From(50m));

        Assert.Equal(149m, invoice.Total.Amount);
    }

    [Fact]
    public void Adicionar_Linha_Sem_Descricao_Lanca_Excecao()
    {
        var invoice = NewInvoice();

        Assert.Throws<DomainRuleException>(() =>
            invoice.AddLine(InvoiceLineType.Subscription, "  ", Money.From(99m)));
    }

    // ---------- Ciclo de vida ----------

    [Fact]
    public void Marcar_Fatura_Como_Paga()
    {
        var invoice = NewInvoice();
        invoice.AddLine(InvoiceLineType.Subscription, "Plano", Money.From(99m));

        invoice.MarkPaid("pay_123", Now);

        Assert.True(invoice.IsPaid);
        Assert.Equal(Now, invoice.PaidAt);
        Assert.Equal("pay_123", invoice.GatewayPaymentId);
    }

    [Fact]
    public void Fatura_Paga_Nao_Recebe_Nova_Linha()
    {
        var invoice = NewInvoice();
        invoice.MarkPaid("pay_123", Now);

        Assert.Throws<DomainRuleException>(() =>
            invoice.AddLine(InvoiceLineType.Ai, "Consumo", Money.From(10m)));
    }

    [Fact]
    public void Pagar_Fatura_Duas_Vezes_Lanca_Excecao()
    {
        var invoice = NewInvoice();
        invoice.MarkPaid("pay_123", Now);

        Assert.Throws<DomainRuleException>(() => invoice.MarkPaid("pay_456", Now));
    }

    [Fact]
    public void Marcar_Fatura_Como_Falha()
    {
        var invoice = NewInvoice();

        invoice.MarkFailed();

        Assert.Equal(InvoiceStatus.Failed, invoice.Status);
    }

    [Fact]
    public void Fatura_Paga_Nao_Pode_Falhar()
    {
        var invoice = NewInvoice();
        invoice.MarkPaid("pay_123", Now);

        Assert.Throws<DomainRuleException>(() => invoice.MarkFailed());
    }

    [Fact]
    public void Fatura_Vencida_Eh_Detectada()
    {
        var invoice = NewInvoice();

        Assert.False(invoice.IsOverdue(DueDate));
        Assert.True(invoice.IsOverdue(DueDate.AddDays(1)));
    }

    [Fact]
    public void Fatura_Paga_Nao_Eh_Vencida()
    {
        var invoice = NewInvoice();
        invoice.MarkPaid("pay_123", Now);

        Assert.False(invoice.IsOverdue(DueDate.AddDays(30)));
    }
}
