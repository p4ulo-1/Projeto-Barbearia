using Servia.Domain.Ai;
using Servia.Domain.Billing;
using Servia.Domain.Common;
using Servia.Domain.Sites;
using Servia.Domain.Tenancy;

namespace Servia.Tests.Ai;

/// <summary>
/// F8.5 — log de operações.
/// F8.7 — limite de personalização por plano.
/// F8.8 — cobrança de alterações pós-lançamento.
/// F8.9 — franquia de IA.
/// F8.12 — fallback.
/// </summary>
public class AiGenerationPolicyTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateOnly PeriodStart = new(2026, 11, 1);
    private static readonly DateOnly PeriodEnd = new(2026, 11, 30);
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private static Plan BasicPlan() =>
        Plan.Create("ESSENCIAL", "Essencial", Money.From(99m), 3,
            SiteCustomizationLevel.Basic, includedAiUnits: 100);

    private static Plan FullPlan() =>
        Plan.Create("BUSINESS", "Business", Money.From(499m), 0,
            SiteCustomizationLevel.Full, includedAiUnits: 5000);

    private static UsageQuota NewQuota(int included = 100) =>
        UsageQuota.Create(TenantId, QuotaResource.AiUnits, PeriodStart, PeriodEnd, included);

    // ---------- F8.7: personalizacao por plano ----------

    [Fact]
    public void Plano_Basico_Permite_Personalizacao_Basica()
    {
        AiGenerationPolicy.EnsureCustomizationAllowed(BasicPlan(), SiteCustomizationLevel.Basic);
    }

    [Fact]
    public void Plano_Basico_Rejeita_Personalizacao_Avancada()
    {
        var exception = Assert.Throws<DomainRuleException>(() =>
            AiGenerationPolicy.EnsureCustomizationAllowed(BasicPlan(), SiteCustomizationLevel.Advanced));

        Assert.Contains("upgrade", exception.Message);
    }

    [Fact]
    public void Plano_Completo_Permite_Qualquer_Personalizacao()
    {
        AiGenerationPolicy.EnsureCustomizationAllowed(FullPlan(), SiteCustomizationLevel.Full);
    }

    // ---------- F8.8: cobranca pos-lancamento ----------

    [Fact]
    public void Geracao_Inicial_Nao_Eh_Cobrada()
    {
        Assert.False(AiGenerationPolicy.IsChargeable(AiOperationType.SiteGeneration));
    }

    [Fact]
    public void Ajuste_Conversacional_Nao_Eh_Cobrado()
    {
        Assert.False(AiGenerationPolicy.IsChargeable(AiOperationType.SiteRefinement));
    }

    [Fact]
    public void Alteracao_Pos_Lancamento_Eh_Cobrada()
    {
        Assert.True(AiGenerationPolicy.IsChargeable(AiOperationType.PostLaunchChange));
    }

    // ---------- F8.9: franquia ----------

    [Fact]
    public void Franquia_Suficiente_Permite_Geracao()
    {
        var quota = NewQuota(100);

        AiGenerationPolicy.EnsureQuotaAvailable(quota, 10);
    }

    [Fact]
    public void Franquia_Insuficiente_Lanca_Excecao()
    {
        var quota = NewQuota(5);

        var exception = Assert.Throws<DomainRuleException>(() =>
            AiGenerationPolicy.EnsureQuotaAvailable(quota, 10));

        Assert.Contains("insuficiente", exception.Message);
    }

    [Fact]
    public void Consumir_Unidades_Reduz_Saldo()
    {
        var quota = NewQuota(100);

        Assert.True(AiGenerationPolicy.TryConsume(quota, 10));
        Assert.Equal(90, quota.Remaining);
    }

    [Fact]
    public void Consumir_Alem_Do_Saldo_Retorna_Falso()
    {
        var quota = NewQuota(5);

        Assert.False(AiGenerationPolicy.TryConsume(quota, 10));
        Assert.Equal(5, quota.Remaining);
    }

    [Fact]
    public void Cota_De_Outro_Recurso_Eh_Rejeitada()
    {
        var whatsAppQuota = UsageQuota.Create(
            TenantId, QuotaResource.WhatsAppMessages, PeriodStart, PeriodEnd, 100);

        Assert.Throws<DomainRuleException>(() =>
            AiGenerationPolicy.EnsureQuotaAvailable(whatsAppQuota, 10));
    }

    // ---------- F8.12: fallback ----------

    [Fact]
    public void Falha_Na_Geracao_Usa_Fallback()
    {
        var result = AiGenerationResult.Failed("Provedor indisponível");

        Assert.True(AiGenerationPolicy.ShouldUseFallback(result));
    }

    [Fact]
    public void Geracao_Bem_Sucedida_Nao_Usa_Fallback()
    {
        var changeSet = AiChangeSet.Create("Proposta", SiteTheme.Default);
        var result = AiGenerationResult.Generated(changeSet, 10);

        Assert.False(AiGenerationPolicy.ShouldUseFallback(result));
    }

    // ---------- F8.5: log ----------

    [Fact]
    public void Registrar_Operacao_Bem_Sucedida()
    {
        var log = AiOperationLog.Record(
            TenantId, AiOperationType.SiteGeneration, "provedor-x", 10, true, Now);

        Assert.Equal(10, log.UnitsConsumed);
        Assert.True(log.Success);
        Assert.Null(log.FailureReason);
    }

    [Fact]
    public void Registrar_Falha_Exige_Motivo()
    {
        Assert.Throws<DomainRuleException>(() =>
            AiOperationLog.Record(TenantId, AiOperationType.SiteGeneration, "provedor-x", 0, false, Now));
    }

    [Fact]
    public void Registrar_Falha_Com_Motivo()
    {
        var log = AiOperationLog.Record(
            TenantId, AiOperationType.SiteGeneration, "provedor-x", 0, false, Now, "Timeout");

        Assert.False(log.Success);
        Assert.Equal("Timeout", log.FailureReason);
    }

    [Fact]
    public void Consumo_Negativo_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            AiOperationLog.Record(TenantId, AiOperationType.SiteGeneration, "provedor-x", -1, true, Now));
    }

    [Fact]
    public void Log_De_Operacao_Eh_Imutavel()
    {
        var methods = typeof(AiOperationLog)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(method => !method.IsSpecialName)
            .Select(method => method.Name)
            .ToArray();

        Assert.DoesNotContain("Update", methods);
        Assert.DoesNotContain("Delete", methods);
        Assert.DoesNotContain("Change", methods);
    }
}
