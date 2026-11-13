namespace Servia.Domain.Sites;

/// <summary>
/// Leitura pública de sites (item 3).
///
/// <b>Resolução por domínio:</b> o estabelecimento é identificado pelo
/// subdomínio (ou domínio próprio) da requisição, nunca por um identificador
/// que o cliente informa. Isso é o oposto das operações administrativas,
/// onde o tenant vem da claim do token.
///
/// <b>Somente publicado:</b> este contrato expõe apenas a versão publicada
/// vigente. Rascunhos e propostas não aprovadas nunca são visíveis
/// publicamente.
///
/// <b>Por que no domínio:</b> a infraestrutura implementa sem depender da
/// aplicação (ADR-004). A leitura atravessa um tenant que ainda não está no
/// contexto, por isso é uma consulta explícita.
/// </summary>
public interface IPublicSiteReader
{
    /// <summary>
    /// Resolve o site publicado pelo subdomínio ou domínio próprio.
    /// Retorna nulo quando nenhum estabelecimento publicado corresponde ao host.
    /// </summary>
    Task<PublicSite?> FindPublishedByHostAsync(
        string host,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Site publicado resolvido para renderização pública (item 3).
///
/// Contém apenas informação destinada à apresentação: tema, páginas e
/// seções da versão publicada. Nada de rascunho, proposta ou dado interno.
/// </summary>
public sealed record PublicSite(
    Guid TenantId,
    string Subdomain,
    string BusinessName,
    int Version,
    SiteTheme Theme,
    IReadOnlyList<PublicSitePage> Pages);

/// <summary>Página publicada.</summary>
public sealed record PublicSitePage(
    string Slug,
    string Title,
    int Order,
    IReadOnlyList<PublicSiteSection> Sections);

/// <summary>Seção publicada, com a configuração do componente.</summary>
public sealed record PublicSiteSection(
    SiteComponentType ComponentType,
    int Order,
    IReadOnlyDictionary<string, string> Configuration);
