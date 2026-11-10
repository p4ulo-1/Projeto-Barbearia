using Servia.Domain.Billing;
using Servia.Domain.Common;
using Servia.Domain.Messaging;

namespace Servia.Tests.Messaging;

/// <summary>
/// F6.9 — limite de consumo do WhatsApp.
/// F6.10 — operação como Solution Partner.
/// F6.11 — repasse da franquia gratuita da Meta.
/// F6.12 — limites definidos pelo estabelecimento.
/// F6.13 — permissões por tipo de mensagem.
/// </summary>
public class MessagingPolicyTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateOnly PeriodStart = new(2026, 11, 1);
    private static readonly DateOnly PeriodEnd = new(2026, 11, 30);
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private static UsageQuota NewQuota(int included = 100) =>
        UsageQuota.Create(TenantId, QuotaResource.WhatsAppMessages, PeriodStart, PeriodEnd, included);

    // ---------- F6.13: permissoes por tipo ----------

    [Fact]
    public void Tipos_Operacionais_Sao_Permitidos_Por_Padrao()
    {
        var policy = MessagingPolicy.Create(TenantId);

        Assert.True(policy.IsTypeAllowed(MessageType.AppointmentConfirmation));
        Assert.True(policy.IsTypeAllowed(MessageType.AppointmentReminder));
        Assert.True(policy.IsTypeAllowed(MessageType.AppointmentChange));
        Assert.True(policy.IsTypeAllowed(MessageType.AutomatedReply));
        Assert.True(policy.IsTypeAllowed(MessageType.Manual));
    }

    [Fact]
    public void Marketing_Eh_Opt_In()
    {
        var policy = MessagingPolicy.Create(TenantId);

        Assert.False(policy.IsTypeAllowed(MessageType.Marketing));
    }

    [Fact]
    public void Autorizar_Tipo_De_Mensagem()
    {
        var policy = MessagingPolicy.Create(TenantId);

        policy.AllowType(MessageType.Marketing);

        Assert.True(policy.IsTypeAllowed(MessageType.Marketing));
    }

    [Fact]
    public void Revogar_Tipo_De_Mensagem()
    {
        var policy = MessagingPolicy.Create(TenantId);

        policy.DisallowType(MessageType.Manual);

        Assert.False(policy.IsTypeAllowed(MessageType.Manual));
    }

    [Fact]
    public void Envio_De_Tipo_Nao_Autorizado_Eh_Rejeitado()
    {
        var policy = MessagingPolicy.Create(TenantId);
        var quota = NewQuota();

        Assert.Throws<DomainRuleException>(() =>
            policy.EnsureCanSend(MessageType.Marketing, quota));
    }

    // ---------- F6.9: limite de consumo ----------

    [Fact]
    public void Envio_Consome_Uma_Mensagem()
    {
        var policy = MessagingPolicy.Create(TenantId);
        var quota = NewQuota(10);

        Assert.True(policy.TryConsumeForSend(MessageType.AppointmentConfirmation, quota));
        Assert.Equal(1, quota.Consumed);
    }

    [Fact]
    public void Envio_Bloqueado_Ao_Atingir_O_Limite()
    {
        var policy = MessagingPolicy.Create(TenantId);
        var quota = NewQuota(2);

        Assert.True(policy.TryConsumeForSend(MessageType.Manual, quota));
        Assert.True(policy.TryConsumeForSend(MessageType.Manual, quota));
        Assert.False(policy.TryConsumeForSend(MessageType.Manual, quota));
    }

    [Fact]
    public void EnsureCanSend_Rejeita_Quando_Cota_Esgotada()
    {
        var policy = MessagingPolicy.Create(TenantId);
        var quota = NewQuota(1);
        quota.TryConsume(1);

        var exception = Assert.Throws<DomainRuleException>(() =>
            policy.EnsureCanSend(MessageType.Manual, quota));

        Assert.Contains("limite", exception.Message);
    }

    [Fact]
    public void Envio_De_Tipo_Nao_Autorizado_Nao_Consome_Cota()
    {
        var policy = MessagingPolicy.Create(TenantId);
        var quota = NewQuota(10);

        Assert.False(policy.TryConsumeForSend(MessageType.Marketing, quota));
        Assert.Equal(0, quota.Consumed);
    }

    [Fact]
    public void Cota_De_Outro_Recurso_Eh_Rejeitada()
    {
        var policy = MessagingPolicy.Create(TenantId);
        var aiQuota = UsageQuota.Create(
            TenantId, QuotaResource.AiUnits, PeriodStart, PeriodEnd, 100);

        Assert.Throws<DomainRuleException>(() =>
            policy.EnsureCanSend(MessageType.Manual, aiQuota));
    }

    // ---------- F6.11: franquia gratuita da Meta ----------

    [Fact]
    public void Franquia_Gratuita_Eh_Repassada_Ao_Tenant()
    {
        // A franquia da Meta entra como `Included` na cota do tenant:
        // nao e retida pela plataforma.
        var quota = NewQuota(included: 1000);

        Assert.Equal(1000, quota.Included);
        Assert.Equal(1000, quota.Remaining);
    }

    // ---------- F6.12: teto do estabelecimento ----------

    [Fact]
    public void Teto_Do_Estabelecimento_Limita_Envio()
    {
        var policy = MessagingPolicy.Create(TenantId);
        var quota = NewQuota(100);
        quota.SetTenantLimit(2);

        Assert.True(policy.TryConsumeForSend(MessageType.Manual, quota));
        Assert.True(policy.TryConsumeForSend(MessageType.Manual, quota));
        Assert.False(policy.TryConsumeForSend(MessageType.Manual, quota));
    }

    // ---------- F6.10: Solution Partner ----------

    [Fact]
    public void Operacao_Nao_Aprovada_Nao_Pode_Operar()
    {
        var operation = WhatsAppPartnerOperation.NotApproved();

        Assert.False(operation.CanOperate);
        Assert.Throws<DomainRuleException>(() => operation.EnsureCanOperate());
    }

    [Fact]
    public void Aprovacao_Sem_Linha_De_Credito_Nao_Basta()
    {
        var operation = WhatsAppPartnerOperation.NotApproved();
        operation.Approve(Now);

        Assert.True(operation.IsApproved);
        Assert.False(operation.CanOperate);
        Assert.Throws<DomainRuleException>(() => operation.EnsureCanOperate());
    }

    [Fact]
    public void Aprovacao_Com_Linha_De_Credito_Permite_Operar()
    {
        var operation = WhatsAppPartnerOperation.NotApproved();
        operation.Approve(Now);
        operation.GrantCreditLine();

        Assert.True(operation.CanOperate);
        operation.EnsureCanOperate(); // não lança
    }

    [Fact]
    public void Linha_De_Credito_Exige_Aprovacao_Previa()
    {
        var operation = WhatsAppPartnerOperation.NotApproved();

        Assert.Throws<DomainRuleException>(() => operation.GrantCreditLine());
    }

    [Fact]
    public void Revogar_Linha_De_Credito_Suspende_Operacao()
    {
        var operation = WhatsAppPartnerOperation.NotApproved();
        operation.Approve(Now);
        operation.GrantCreditLine();

        operation.RevokeCreditLine();

        Assert.False(operation.CanOperate);
    }

    [Fact]
    public void Aprovar_Duas_Vezes_Lanca_Excecao()
    {
        var operation = WhatsAppPartnerOperation.NotApproved();
        operation.Approve(Now);

        Assert.Throws<DomainRuleException>(() => operation.Approve(Now));
    }
}
