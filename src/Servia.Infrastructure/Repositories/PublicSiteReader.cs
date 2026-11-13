using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Servia.Domain.Sites;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;

namespace Servia.Infrastructure.Repositories;

/// <summary>
/// Implementação EF Core de <see cref="IPublicSiteReader"/> (item 3).
///
/// <b>Por que IgnoreQueryFilters:</b> a requisição pública ainda não tem
/// tenant no contexto — é justamente o host que o resolve. O filtro global
/// negaria tudo. A consulta é explícita e restrita a sites publicados, então
/// não expõe rascunho nem dado de outro estabelecimento.
///
/// <b>Somente publicado:</b> só retorna quando o site está publicado e a
/// versão vigente existe. O snapshot da publicação é a fonte — o rascunho
/// nunca é servido.
/// </summary>
public sealed class PublicSiteReader(AppDbContext context) : IPublicSiteReader
{
    public async Task<PublicSite?> FindPublishedByHostAsync(
        string host,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeHost(host);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        // O host pode ser o subdominio (barbearia.minhaplataforma.app) ou o
        // dominio proprio (barbearia.com).
        //
        // O Subdomain guarda apenas o rotulo (barbearia). Por isso comparamos
        // o primeiro rotulo do host com o Subdomain e o host inteiro com o
        // CustomDomain. Nao fixamos o dominio da plataforma no codigo: o
        // primeiro rotulo e extraido do proprio host.
        var firstLabel = FirstLabel(normalized);

        var site = await context.Sites
            .IgnoreQueryFilters()
            .Where(item => item.Subdomain == normalized
                || item.Subdomain == firstLabel
                || (item.CustomDomain != null && item.CustomDomain == normalized))
            .FirstOrDefaultAsync(cancellationToken);

        // Site de tenant suspenso/cancelado nao e servido publicamente.
        if (site is null || !site.IsPublished || site.PublishedVersionId is null)
        {
            return null;
        }

        var tenant = await context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.Id == site.TenantId, cancellationToken);

        if (tenant is null || !tenant.CanRead)
        {
            return null;
        }

        var publication = site.Publications
            .FirstOrDefault(item => item.Id == site.PublishedVersionId);

        if (publication is null)
        {
            return null;
        }

        var pages = ParseSnapshot(publication.Snapshot);

        if (pages.Count == 0)
        {
            return null;
        }

        return new PublicSite(
            tenant.Id,
            site.Subdomain,
            tenant.Name,
            publication.Version,
            site.Theme,
            pages);
    }

    /// <summary>
    /// Reduz o host ao formato comparavel: remove esquema, caminho e porta.
    /// O sufixo do dominio da plataforma e tratado por <see cref="FirstLabel"/>,
    /// porque o codigo nao fixa qual e esse dominio.
    /// </summary>
    private static string NormalizeHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return string.Empty;
        }

        var value = host.Trim().ToLowerInvariant();

        var schemeIndex = value.IndexOf("://", StringComparison.Ordinal);
        if (schemeIndex >= 0)
        {
            value = value[(schemeIndex + 3)..];
        }

        var pathIndex = value.IndexOf('/');
        if (pathIndex >= 0)
        {
            value = value[..pathIndex];
        }

        var portIndex = value.IndexOf(':');
        if (portIndex >= 0)
        {
            value = value[..portIndex];
        }

        // Remove o prefixo www: e um alias de apresentacao, nao identifica
        // o estabelecimento.
        if (value.StartsWith("www.", StringComparison.Ordinal))
        {
            value = value[4..];
        }

        return value;
    }

    /// <summary>
    /// Extrai o primeiro rotulo do host (o subdominio).
    ///
    /// <c>barbearia.servia.app</c> → <c>barbearia</c>. Host com um unico
    /// rotulo (<c>barbearia.com</c>, dominio proprio) ou IP retorna o proprio
    /// valor, para nao confundir dominio proprio com subdominio.
    /// </summary>
    private static string FirstLabel(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return string.Empty;
        }

        var dotIndex = host.IndexOf('.');

        if (dotIndex <= 0)
        {
            return host;
        }

        return host[..dotIndex];
    }

    private static IReadOnlyList<PublicSitePage> ParseSnapshot(string snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(snapshot);
            var root = document.RootElement;

            if (!root.TryGetProperty("pages", out var pagesElement)
                || pagesElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var pages = new List<PublicSitePage>();

            foreach (var pageElement in pagesElement.EnumerateArray())
            {
                var slug = pageElement.TryGetProperty("slug", out var slugValue)
                    ? slugValue.GetString() ?? string.Empty
                    : string.Empty;

                var title = pageElement.TryGetProperty("title", out var titleValue)
                    ? titleValue.GetString() ?? string.Empty
                    : string.Empty;

                var order = pageElement.TryGetProperty("order", out var orderValue)
                    && orderValue.TryGetInt32(out var parsedOrder)
                    ? parsedOrder
                    : 0;

                var sections = new List<PublicSiteSection>();

                if (pageElement.TryGetProperty("sections", out var sectionsElement)
                    && sectionsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var sectionElement in sectionsElement.EnumerateArray())
                    {
                        if (!sectionElement.TryGetProperty("type", out var typeValue)
                            || !Enum.TryParse<SiteComponentType>(
                                typeValue.GetString(), ignoreCase: true, out var componentType))
                        {
                            // Componente desconhecido nao quebra a pagina; e ignorado.
                            continue;
                        }

                        var sectionOrder = sectionElement.TryGetProperty("order", out var sectionOrderValue)
                            && sectionOrderValue.TryGetInt32(out var parsedSectionOrder)
                            ? parsedSectionOrder
                            : 0;

                        var configuration = new Dictionary<string, string>();

                        if (sectionElement.TryGetProperty("configuration", out var configElement)
                            && configElement.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var property in configElement.EnumerateObject())
                            {
                                configuration[property.Name] = property.Value.GetString() ?? string.Empty;
                            }
                        }

                        sections.Add(new PublicSiteSection(
                            componentType,
                            sectionOrder,
                            configuration));
                    }
                }

                pages.Add(new PublicSitePage(
                    slug, title, order, sections.OrderBy(section => section.Order).ToList()));
            }

            return pages.OrderBy(page => page.Order).ToList();
        }
        catch (JsonException)
        {
            // Snapshot corrompido: nunca serve conteudo parcial.
            return [];
        }
    }
}
