using Servia.Domain.Common;
using Servia.Domain.Sites;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Ai;

/// <summary>
/// Requisição de geração de site por IA (F8.2).
///
/// Reúne o contexto que a IA precisa: segmento, nome, referências visuais e
/// preferências do estabelecimento. Quanto mais específico o contexto, menos
/// genérica a proposta.
///
/// O nível de personalização é limitado pelo plano (F8.7): a requisição não
/// pode pedir mais do que o plano contratado permite.
/// </summary>
public sealed record AiGenerationRequest(
    Guid TenantId,
    string BusinessName,
    SegmentType Segment,
    SiteCustomizationLevel CustomizationLevel,
    IReadOnlyList<string> ReferenceUrls,
    string? Preferences)
{
    /// <summary>
    /// Cria a requisição validando o contexto mínimo.
    /// </summary>
    public static AiGenerationRequest Create(
        Guid tenantId,
        string businessName,
        SegmentType segment,
        SiteCustomizationLevel customizationLevel,
        IReadOnlyList<string>? referenceUrls = null,
        string? preferences = null)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(businessName))
        {
            throw new DomainRuleException("O nome do estabelecimento é obrigatório.");
        }

        var references = referenceUrls ?? [];

        if (references.Count > 5)
        {
            throw new DomainRuleException("São permitidas no máximo 5 referências visuais.");
        }

        foreach (var url in references)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed)
                || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
            {
                throw new DomainRuleException($"Referência inválida: {url}.");
            }
        }

        return new AiGenerationRequest(
            tenantId,
            businessName.Trim(),
            segment,
            customizationLevel,
            references,
            string.IsNullOrWhiteSpace(preferences) ? null : preferences.Trim());
    }
}
