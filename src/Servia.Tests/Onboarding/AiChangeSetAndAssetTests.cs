using Servia.Domain.Ai;
using Servia.Domain.Common;
using Servia.Domain.Sites;

namespace Servia.Tests.Onboarding;

/// <summary>
/// Guardrails da proposta gerada pela IA e validação de arquivos (itens 1.3, 1.4).
/// </summary>
public sealed class AiChangeSetAndAssetTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Componente_Fora_Da_Biblioteca_E_Rejeitado()
    {
        var changeSet = AiChangeSet.Create(Guid.NewGuid(), "Site", SiteTheme.Default);

        // 9999 não é um SiteComponentType válido.
        Assert.Throws<DomainRuleException>(() =>
            changeSet.AddSection((SiteComponentType)9999, new Dictionary<string, string>()));
    }

    [Fact]
    public void Propriedade_Fora_Do_Schema_E_Rejeitada()
    {
        var changeSet = AiChangeSet.Create(Guid.NewGuid(), "Site", SiteTheme.Default);

        Assert.Throws<DomainRuleException>(() =>
            changeSet.AddSection(SiteComponentType.Hero, new Dictionary<string, string>
            {
                ["title"] = "Bem-vindo",
                ["codigoArbitrario"] = "<script>"
            }));
    }

    [Fact]
    public void Proposta_Com_Componente_Valido_E_Completa()
    {
        var changeSet = AiChangeSet.Create(Guid.NewGuid(), "Site", SiteTheme.Default);

        changeSet.AddSection(SiteComponentType.Hero, new Dictionary<string, string>
        {
            ["title"] = "Barbearia Exemplo"
        });

        Assert.True(changeSet.IsComplete());
    }

    [Fact]
    public void Proposta_Sem_Propriedade_Obrigatoria_E_Incompleta()
    {
        var changeSet = AiChangeSet.Create(Guid.NewGuid(), "Site", SiteTheme.Default);

        // Hero exige "title".
        changeSet.AddSection(SiteComponentType.Hero, new Dictionary<string, string>
        {
            ["subtitle"] = "sem título"
        });

        Assert.False(changeSet.IsComplete());
    }

    [Fact]
    public void Proposta_Sem_Tenant_E_Rejeitada()
    {
        Assert.Throws<DomainRuleException>(() =>
            AiChangeSet.Create(Guid.Empty, "Site", SiteTheme.Default));
    }

    [Fact]
    public void Arquivo_Com_Extensao_Invalida_E_Rejeitado()
    {
        Assert.Throws<DomainRuleException>(() => SiteAsset.Register(
            Guid.NewGuid(), null, SiteAssetKind.Logo,
            "logo.exe", "application/octet-stream", 100,
            "path/logo.exe", "checksum", Now));
    }

    [Fact]
    public void Arquivo_Acima_Do_Limite_E_Rejeitado()
    {
        Assert.Throws<DomainRuleException>(() => SiteAsset.Register(
            Guid.NewGuid(), null, SiteAssetKind.Logo,
            "logo.png", "image/png", SiteAsset.MaxSizeBytes + 1,
            "path/logo.png", "checksum", Now));
    }

    [Fact]
    public void Arquivo_Nasce_Pendente_E_Somente_Aprovado_E_Usavel()
    {
        var asset = SiteAsset.Register(
            Guid.NewGuid(), null, SiteAssetKind.Logo,
            "logo.png", "image/png", 100,
            "path/logo.png", "checksum", Now);

        Assert.Equal(SiteAssetStatus.Pending, asset.Status);
        Assert.False(asset.IsUsable);

        asset.Approve();

        Assert.True(asset.IsUsable);
    }

    [Fact]
    public void Arquivo_Rejeitado_Nao_Pode_Ser_Aprovado()
    {
        var asset = SiteAsset.Register(
            Guid.NewGuid(), null, SiteAssetKind.Logo,
            "logo.png", "image/png", 100,
            "path/logo.png", "checksum", Now);

        asset.Reject("Imagem borrada");

        Assert.Throws<DomainRuleException>(asset.Approve);
    }

    [Fact]
    public void Nome_Com_Travessia_De_Caminho_E_Normalizado()
    {
        var asset = SiteAsset.Register(
            Guid.NewGuid(), null, SiteAssetKind.Logo,
            "../../etc/logo.png", "image/png", 100,
            "path/logo.png", "checksum", Now);

        // O nome é reduzido ao arquivo final; não carrega o caminho.
        Assert.Equal("logo.png", asset.FileName);
    }
}
