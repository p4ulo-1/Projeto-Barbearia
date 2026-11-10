using Servia.Domain.Catalog;
using Servia.Domain.Common;

namespace Servia.Tests.Catalog;

/// <summary>
/// Invariantes do catálogo de serviços: nome obrigatório, duração positiva,
/// preço não negativo e desativação que preserva histórico.
/// </summary>
public class ServiceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static Service NewService() =>
        Service.Create(TenantId, "Corte masculino", Money.From(50m), 30);

    [Fact]
    public void Criar_Servico_Valido_Inicia_Ativo()
    {
        var service = NewService();

        Assert.Equal("Corte masculino", service.Name);
        Assert.Equal(30, service.DurationMinutes);
        Assert.Equal(50m, service.Price.Amount);
        Assert.True(service.IsActive);
        Assert.NotEqual(Guid.Empty, service.Id);
    }

    [Fact]
    public void Criar_Servico_Sem_Tenant_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            Service.Create(Guid.Empty, "Corte", Money.From(50m), 30));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_Servico_Sem_Nome_Lanca_Excecao(string name)
    {
        Assert.Throws<DomainRuleException>(() =>
            Service.Create(TenantId, name, Money.From(50m), 30));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-15)]
    public void Criar_Servico_Com_Duracao_Invalida_Lanca_Excecao(int duration)
    {
        Assert.Throws<DomainRuleException>(() =>
            Service.Create(TenantId, "Corte", Money.From(50m), duration));
    }

    [Fact]
    public void Criar_Servico_Com_Preco_Negativo_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() => Money.From(-10m));
    }

    [Fact]
    public void Alterar_Preco_Atualiza_Valor()
    {
        var service = NewService();

        service.ChangePrice(Money.From(75m));

        Assert.Equal(75m, service.Price.Amount);
    }

    [Fact]
    public void Alterar_Duracao_Invalida_Lanca_Excecao()
    {
        var service = NewService();

        Assert.Throws<DomainRuleException>(() => service.ChangeDuration(0));
        Assert.Equal(30, service.DurationMinutes);
    }

    [Fact]
    public void Desativar_Preserva_Dados_Do_Servico()
    {
        var service = NewService();
        var originalId = service.Id;

        service.Deactivate();

        Assert.False(service.IsActive);
        Assert.Equal(originalId, service.Id);
        Assert.Equal("Corte masculino", service.Name);
    }

    [Fact]
    public void Reativar_Torna_Servico_Disponivel_Novamente()
    {
        var service = NewService();
        service.Deactivate();

        service.Activate();

        Assert.True(service.IsActive);
    }

    [Fact]
    public void Atualizar_Sem_Nome_Lanca_Excecao()
    {
        var service = NewService();

        Assert.Throws<DomainRuleException>(() =>
            service.Update("", null, null, false));
    }
}
