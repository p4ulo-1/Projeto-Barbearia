using Servia.Domain.Billing;
using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Tests.Billing;

/// <summary>
/// F4.9 — todos os módulos em todos os planos.
/// F4.10 — upgrade por excedente de profissionais.
/// F4.11 — limite de personalização do site por plano.
/// F4.13 — independência financeiro × billing.
/// </summary>
public class BillingRulesTests
{
    private static Plan Essential() =>
        Plan.Create("ESSENCIAL", "Essencial", Money.From(99m), 3, SiteCustomizationLevel.Basic, 100);

    private static Plan Business() =>
        Plan.Create("BUSINESS", "Business", Money.From(499m), 0, SiteCustomizationLevel.Full, 5000);

    // ---------- F4.9: modulos ----------

    [Fact]
    public void Todos_Os_Modulos_Estao_Disponiveis_Em_Todos_Os_Planos()
    {
        var plans = new[] { Essential(), Business() };

        foreach (var plan in plans)
        {
            foreach (var module in Enum.GetValues<ModuleType>())
            {
                Assert.True(BillingRules.IsModuleAvailable(plan, module),
                    $"O módulo {module} deveria estar disponível no plano {plan.Name}.");
            }
        }
    }

    [Fact]
    public void Plano_Inativo_Nao_Libera_Modulos()
    {
        var plan = Essential();
        plan.Deactivate();

        Assert.False(BillingRules.IsModuleAvailable(plan, ModuleType.AiSiteBuilder));
    }

    // ---------- F4.10: upgrade por excedente ----------

    [Fact]
    public void Plano_Comporta_Profissionais_Dentro_Do_Limite()
    {
        var plan = Essential();

        Assert.False(BillingRules.RequiresUpgrade(plan, 3));
    }

    [Fact]
    public void Excedente_De_Profissionais_Exige_Upgrade()
    {
        var plan = Essential();

        Assert.True(BillingRules.RequiresUpgrade(plan, 4));
    }

    [Fact]
    public void Plano_Ilimitado_Nao_Exige_Upgrade()
    {
        var plan = Business();

        Assert.False(BillingRules.RequiresUpgrade(plan, 1000));
    }

    // ---------- F4.11: personalizacao do site ----------

    [Fact]
    public void Plano_Basico_Nao_Permite_Personalizacao_Avancada()
    {
        var plan = Essential();

        BillingRules.EnsureCustomizationAllowed(plan, SiteCustomizationLevel.Basic);

        Assert.Throws<DomainRuleException>(() =>
            BillingRules.EnsureCustomizationAllowed(plan, SiteCustomizationLevel.Advanced));
    }

    [Fact]
    public void Plano_Completo_Permite_Qualquer_Personalizacao()
    {
        var plan = Business();

        BillingRules.EnsureCustomizationAllowed(plan, SiteCustomizationLevel.Full);
        BillingRules.EnsureCustomizationAllowed(plan, SiteCustomizationLevel.Basic);
    }

    // ---------- F4.13: independencia ----------

    [Fact]
    public void Billing_Nao_Referencia_Financeiro_Do_Tenant()
    {
        // As regras de billing só conhecem Plan e UsageQuota.
        // Nenhum tipo do financeiro do tenant (Payment, Transaction,
        // CommissionEntry) aparece nas assinaturas públicas.
        var methods = typeof(BillingRules)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        var parameterTypes = methods
            .SelectMany(method => method.GetParameters())
            .Select(parameter => parameter.ParameterType.Name)
            .ToArray();

        Assert.DoesNotContain("Payment", parameterTypes);
        Assert.DoesNotContain("Transaction", parameterTypes);
        Assert.DoesNotContain("CommissionEntry", parameterTypes);
    }

    [Fact]
    public void Fatura_Da_Plataforma_Pertence_Ao_Tenant_Mas_Nao_Ao_Financeiro()
    {
        var invoice = PlatformInvoice.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), new DateOnly(2026, 12, 5));

        // A fatura é escopada por tenant, mas é um agregado de billing.
        Assert.NotEqual(Guid.Empty, invoice.TenantId);
        Assert.IsType<PlatformInvoice>(invoice);
    }
}
