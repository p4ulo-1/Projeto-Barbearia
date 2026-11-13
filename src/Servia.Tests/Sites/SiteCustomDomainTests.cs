using Servia.Domain.Common;
using Servia.Domain.Sites;

namespace Servia.Tests.Sites;

/// <summary>
/// Domínio próprio do site (melhoria da auditoria): normalização e validação.
///
/// Prova que o domínio é gravado em minúsculas, validado quanto ao formato e
/// que pode ser removido.
/// </summary>
public sealed class SiteCustomDomainTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static Site NewSite() => Site.Create(TenantId, "barbearia-xyz");

    [Fact]
    public void Define_Dominio_Normalizado_Para_Minusculas()
    {
        var site = NewSite();

        site.SetCustomDomain("  Barbearia.Exemplo.COM  ");

        Assert.Equal("barbearia.exemplo.com", site.CustomDomain);
    }

    [Fact]
    public void Dominio_Nulo_Remove_O_Dominio_Proprio()
    {
        var site = NewSite();
        site.SetCustomDomain("barbearia.exemplo.com");

        site.ClearCustomDomain();

        Assert.Null(site.CustomDomain);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sem-ponto")]
    [InlineData("-invalido.com")]
    [InlineData("invalido-.com")]
    [InlineData("domínio.com")]
    public void Dominio_Invalido_E_Rejeitado(string domain)
    {
        var site = NewSite();

        Assert.Throws<DomainRuleException>(() => site.SetCustomDomain(domain));
    }

    [Fact]
    public void Variacoes_De_Caixa_Produzem_O_Mesmo_Valor_Armazenado()
    {
        var a = NewSite();
        var b = NewSite();

        a.SetCustomDomain("barbearia.exemplo.com");
        b.SetCustomDomain("BARBEARIA.EXEMPLO.COM");

        Assert.Equal(a.CustomDomain, b.CustomDomain);
    }
}
