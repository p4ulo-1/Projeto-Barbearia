using Servia.Domain.Ai;
using Servia.Domain.Common;
using Servia.Domain.Sites;

namespace Servia.Application.Sites;

/// <summary>
/// Aplica uma alteração direcionada aprovada ao site (C12).
///
/// <b>Cirúrgico:</b> altera apenas os campos indicados, na página e no
/// componente correspondentes. Páginas e elementos fora da solicitação
/// permanecem intactos.
///
/// <b>Somente aprovado:</b> recusa aplicar uma proposta não aprovada.
///
/// <b>Validação:</b> a estrutura da proposta é validada antes de tocar o site.
/// </summary>
public static class SiteChangeApplier
{
    /// <summary>
    /// Aplica a alteração aprovada ao site. Retorna o número de campos
    /// efetivamente alterados.
    /// </summary>
    public static int Apply(Site site, AiSiteChange change)
    {
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(change);

        if (change.Status != AiSiteChangeStatus.Approved)
        {
            throw new DomainRuleException(
                "Somente uma alteração aprovada pode ser aplicada ao site.");
        }

        change.EnsureValid();

        var applied = 0;

        foreach (var pageChange in change.Pages)
        {
            var page = site.Pages.FirstOrDefault(
                item => string.Equals(item.Slug, pageChange.PageSlug, StringComparison.Ordinal));

            if (page is null)
            {
                // A pagina alvo nao existe: nao criamos nem alteramos nada
                // fora do que foi solicitado.
                continue;
            }

            foreach (var field in pageChange.Fields)
            {
                var section = page.Sections.FirstOrDefault(
                    item => item.ComponentType == field.ComponentType);

                if (section is null)
                {
                    // Componente ausente na pagina: nao inventamos secao.
                    continue;
                }

                section.SetProperty(field.FieldKey, field.ProposedValue);
                applied++;
            }
        }

        return applied;
    }
}
