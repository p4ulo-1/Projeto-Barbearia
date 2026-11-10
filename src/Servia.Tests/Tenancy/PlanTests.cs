using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Tests.Tenancy;

/// <summary>
/// Invariantes do plano comercial: faixas de profissionais, grau de
/// personalização e ausência de preço por excedente.
/// </summary>
public class PlanTests
{
    private static Plan Essential() =>
        Plan.Create("ESSENCIAL", "Essencial", Money.From(99m), 3, SiteCustomizationLevel.Basic, 100);

    private static Plan Business() =>
        Plan.Create("BUSINESS", "Business", Money.From(499m), 0, SiteCustomizationLevel.Full, 5000);

    [Fact]
    public void Criar_Plano_Valido_Inicia_Ativo()
    {
        var plan = Essential();

        Assert.Equal("ESSENCIAL", plan.Code);
        Assert.Equal("Essencial", plan.Name);
        Assert.Equal(99m, plan.MonthlyPrice.Amount);
        Assert.Equal(3, plan.IncludedProfessionals);
        Assert.True(plan.IsActive);
    }

    [Fact]
    public void Codigo_Eh_Normalizado_Para_Maiusculas()
    {
        var plan = Plan.Create("pro", "Pro", Money.From(199m), 7, SiteCustomizationLevel.Intermediate, 500);

        Assert.Equal("PRO", plan.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_Plano_Sem_Codigo_Lanca_Excecao(string code)
    {
        Assert.Throws<DomainRuleException>(() =>
            Plan.Create(code, "Plano", Money.From(99m), 3, SiteCustomizationLevel.Basic, 100));
    }

    [Fact]
    public void Criar_Plano_Sem_Nome_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            Plan.Create("X", "", Money.From(99m), 3, SiteCustomizationLevel.Basic, 100));
    }

    [Fact]
    public void Criar_Plano_Com_Profissionais_Negativos_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            Plan.Create("X", "Plano", Money.From(99m), -1, SiteCustomizationLevel.Basic, 100));
    }

    [Fact]
    public void Criar_Plano_Com_Franquia_Negativa_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            Plan.Create("X", "Plano", Money.From(99m), 3, SiteCustomizationLevel.Basic, -1));
    }

    // ---------- Faixa de profissionais ----------

    [Fact]
    public void Plano_Comporta_Profissionais_Dentro_Do_Limite()
    {
        var plan = Essential();

        Assert.True(plan.AllowsProfessionals(1));
        Assert.True(plan.AllowsProfessionals(3));
    }

    [Fact]
    public void Plano_Nao_Comporta_Profissionais_Acima_Do_Limite()
    {
        var plan = Essential();

        Assert.False(plan.AllowsProfessionals(4));
    }

    [Fact]
    public void Plano_Ilimitado_Comporta_Qualquer_Quantidade()
    {
        var plan = Business();

        Assert.True(plan.HasUnlimitedProfessionals);
        Assert.True(plan.AllowsProfessionals(1000));
    }

    [Fact]
    public void AllowsProfessionals_Rejeita_Quantidade_Negativa()
    {
        Assert.Throws<DomainRuleException>(() => Essential().AllowsProfessionals(-1));
    }

    // ---------- Personalizacao do site ----------

    [Fact]
    public void Plano_Basico_Nao_Permite_Personalizacao_Avancada()
    {
        var plan = Essential();

        Assert.True(plan.AllowsCustomization(SiteCustomizationLevel.Basic));
        Assert.False(plan.AllowsCustomization(SiteCustomizationLevel.Advanced));
    }

    [Fact]
    public void Plano_Completo_Permite_Qualquer_Personalizacao()
    {
        var plan = Business();

        Assert.True(plan.AllowsCustomization(SiteCustomizationLevel.Full));
        Assert.True(plan.AllowsCustomization(SiteCustomizationLevel.Basic));
    }

    // ---------- Operacoes ----------

    [Fact]
    public void Alterar_Preco_Atualiza_Valor()
    {
        var plan = Essential();

        plan.ChangePrice(Money.From(129m));

        Assert.Equal(129m, plan.MonthlyPrice.Amount);
    }

    [Fact]
    public void Desativar_E_Reativar_Alteram_Estado()
    {
        var plan = Essential();

        plan.Deactivate();
        Assert.False(plan.IsActive);

        plan.Activate();
        Assert.True(plan.IsActive);
    }
}
