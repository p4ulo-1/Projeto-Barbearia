using Servia.Domain.Ai;
using Servia.Domain.Common;
using Servia.Domain.Sites;
using Servia.Domain.Tenancy;

namespace Servia.Tests.Ai;

/// <summary>
/// F8.2 — requisição de geração com referências e preferências.
/// </summary>
public class AiGenerationRequestTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public void Criar_Requisicao_Valida()
    {
        var request = AiGenerationRequest.Create(
            TenantId, "Barbearia XYZ", SegmentType.Barbershop,
            SiteCustomizationLevel.Basic, preferences: "Estilo clássico");

        Assert.Equal("Barbearia XYZ", request.BusinessName);
        Assert.Equal(SegmentType.Barbershop, request.Segment);
        Assert.Equal("Estilo clássico", request.Preferences);
        Assert.Empty(request.ReferenceUrls);
    }

    [Fact]
    public void Criar_Requisicao_Sem_Tenant_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            AiGenerationRequest.Create(Guid.Empty, "Barbearia", SegmentType.Barbershop,
                SiteCustomizationLevel.Basic));
    }

    [Fact]
    public void Criar_Requisicao_Sem_Nome_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            AiGenerationRequest.Create(TenantId, "  ", SegmentType.Barbershop,
                SiteCustomizationLevel.Basic));
    }

    [Fact]
    public void Aceitar_Referencias_Validas()
    {
        var request = AiGenerationRequest.Create(
            TenantId, "Barbearia", SegmentType.Barbershop, SiteCustomizationLevel.Basic,
            ["https://exemplo.com", "https://outro.com"]);

        Assert.Equal(2, request.ReferenceUrls.Count);
    }

    [Fact]
    public void Rejeitar_Referencia_Invalida()
    {
        Assert.Throws<DomainRuleException>(() =>
            AiGenerationRequest.Create(
                TenantId, "Barbearia", SegmentType.Barbershop, SiteCustomizationLevel.Basic,
                ["nao-e-url"]));
    }

    [Fact]
    public void Rejeitar_Referencia_Com_Esquema_Nao_Http()
    {
        Assert.Throws<DomainRuleException>(() =>
            AiGenerationRequest.Create(
                TenantId, "Barbearia", SegmentType.Barbershop, SiteCustomizationLevel.Basic,
                ["ftp://exemplo.com"]));
    }

    [Fact]
    public void Rejeitar_Mais_De_Cinco_Referencias()
    {
        var references = Enumerable.Range(1, 6)
            .Select(index => $"https://exemplo{index}.com")
            .ToArray();

        Assert.Throws<DomainRuleException>(() =>
            AiGenerationRequest.Create(
                TenantId, "Barbearia", SegmentType.Barbershop,
                SiteCustomizationLevel.Basic, references));
    }
}
