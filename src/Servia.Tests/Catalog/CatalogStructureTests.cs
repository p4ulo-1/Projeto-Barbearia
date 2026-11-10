using Servia.Domain.Catalog;
using Servia.Domain.Common;

namespace Servia.Tests.Catalog;

/// <summary>
/// F2.3 — categorias, variações e combos de serviço.
/// </summary>
public class CatalogStructureTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    // ---------- Categoria ----------

    [Fact]
    public void Criar_Categoria_Valida()
    {
        var category = ServiceCategory.Create(TenantId, "Cortes", sortOrder: 1);

        Assert.Equal("Cortes", category.Name);
        Assert.Equal(1, category.SortOrder);
        Assert.Equal(TenantId, category.TenantId);
    }

    [Fact]
    public void Criar_Categoria_Sem_Nome_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() => ServiceCategory.Create(TenantId, "  "));
    }

    [Fact]
    public void Criar_Categoria_Com_Ordem_Negativa_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() => ServiceCategory.Create(TenantId, "Cortes", -1));
    }

    [Fact]
    public void Renomear_Categoria_Altera_Nome()
    {
        var category = ServiceCategory.Create(TenantId, "Cortes");

        category.Rename("Cortes e Barba");

        Assert.Equal("Cortes e Barba", category.Name);
    }

    // ---------- Variacao ----------

    [Fact]
    public void Criar_Variacao_Com_Preco_E_Duracao_Proprios()
    {
        var serviceId = Guid.NewGuid();

        var variant = ServiceVariant.Create(
            TenantId, serviceId, "Cabelo longo", Money.From(80m), 60);

        Assert.Equal("Cabelo longo", variant.Label);
        Assert.Equal(80m, variant.Price.Amount);
        Assert.Equal(60, variant.DurationMinutes);
        Assert.Equal(serviceId, variant.ServiceId);
    }

    [Fact]
    public void Criar_Variacao_Sem_Servico_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            ServiceVariant.Create(TenantId, Guid.Empty, "Longo", Money.From(80m), 60));
    }

    [Fact]
    public void Criar_Variacao_Com_Duracao_Invalida_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            ServiceVariant.Create(TenantId, Guid.NewGuid(), "Longo", Money.From(80m), 0));
    }

    [Fact]
    public void Atualizar_Variacao_Altera_Preco_E_Duracao()
    {
        var variant = ServiceVariant.Create(TenantId, Guid.NewGuid(), "Longo", Money.From(80m), 60);

        variant.Update(Money.From(95m), 75);

        Assert.Equal(95m, variant.Price.Amount);
        Assert.Equal(75, variant.DurationMinutes);
    }

    // ---------- Combo ----------

    [Fact]
    public void Criar_Combo_Valido()
    {
        var combo = ServiceCombo.Create(TenantId, "Corte + Barba", Money.From(70m));

        Assert.Equal("Corte + Barba", combo.Name);
        Assert.Equal(70m, combo.ComboPrice.Amount);
        Assert.Empty(combo.Items);
    }

    [Fact]
    public void Adicionar_Item_Ao_Combo()
    {
        var combo = ServiceCombo.Create(TenantId, "Combo", Money.From(70m));
        var serviceId = Guid.NewGuid();

        combo.AddItem(serviceId, 2);

        Assert.Single(combo.Items);
        Assert.Equal(2, combo.Items.First().Quantity);
    }

    [Fact]
    public void Adicionar_Mesmo_Servico_Incrementa_Quantidade()
    {
        var combo = ServiceCombo.Create(TenantId, "Combo", Money.From(70m));
        var serviceId = Guid.NewGuid();

        combo.AddItem(serviceId, 1);
        combo.AddItem(serviceId, 2);

        Assert.Single(combo.Items);
        Assert.Equal(3, combo.Items.First().Quantity);
    }

    [Fact]
    public void Adicionar_Item_Com_Quantidade_Invalida_Lanca_Excecao()
    {
        var combo = ServiceCombo.Create(TenantId, "Combo", Money.From(70m));

        Assert.Throws<DomainRuleException>(() => combo.AddItem(Guid.NewGuid(), 0));
    }

    [Fact]
    public void Remover_Item_Do_Combo()
    {
        var combo = ServiceCombo.Create(TenantId, "Combo", Money.From(70m));
        var serviceId = Guid.NewGuid();
        combo.AddItem(serviceId);

        combo.RemoveItem(serviceId);

        Assert.Empty(combo.Items);
    }

    [Fact]
    public void Remover_Item_Inexistente_Lanca_Excecao()
    {
        var combo = ServiceCombo.Create(TenantId, "Combo", Money.From(70m));

        Assert.Throws<DomainRuleException>(() => combo.RemoveItem(Guid.NewGuid()));
    }

    [Fact]
    public void Calcular_Duracao_Total_Do_Combo()
    {
        var corte = Guid.NewGuid();
        var barba = Guid.NewGuid();

        var combo = ServiceCombo.Create(TenantId, "Combo", Money.From(70m));
        combo.AddItem(corte, 1);
        combo.AddItem(barba, 2);

        var durations = new Dictionary<Guid, int> { [corte] = 30, [barba] = 15 };

        Assert.Equal(60, combo.TotalDuration(durations));
    }

    [Fact]
    public void Calcular_Duracao_Com_Servico_Desconhecido_Lanca_Excecao()
    {
        var combo = ServiceCombo.Create(TenantId, "Combo", Money.From(70m));
        combo.AddItem(Guid.NewGuid());

        Assert.Throws<DomainRuleException>(() =>
            combo.TotalDuration(new Dictionary<Guid, int>()));
    }
}
