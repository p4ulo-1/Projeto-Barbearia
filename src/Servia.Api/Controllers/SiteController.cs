using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Servia.Api.Authorization;
using Servia.Api.Contracts;
using Servia.Domain.Common;
using Servia.Domain.Identity;
using Servia.Domain.Sites;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;

namespace Servia.Api.Controllers;

/// <summary>
/// Endpoints do site do estabelecimento (F13.5).
///
/// O rascunho é editável; a versão publicada só muda em `publish`. Alterar
/// o rascunho não afeta o site no ar.
/// </summary>
[ApiController]
[Route("api/v1/site")]
[Authorize]
public sealed class SiteController(
    AppDbContext context,
    ITenantContext tenantContext,
    TimeProvider timeProvider) : ControllerBase
{
    /// <summary>Consulta o site do estabelecimento.</summary>
    [HttpGet]
    [RequirePermission(Permission.ViewTenantSettings)]
    [ProducesResponseType<SiteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(CancellationToken cancellationToken)
    {
        var site = await LoadSiteAsync(cancellationToken);

        return Ok(new SiteResponse(
            site.Id,
            site.Subdomain,
            site.Status.ToString(),
            site.IsPublished,
            site.Pages.Count,
            site.Publications.Count));
    }

    /// <summary>Cria o site do estabelecimento.</summary>
    [HttpPost]
    [RequirePermission(Permission.ManageTenantSettings)]
    [ProducesResponseType<SiteResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateSiteRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var site = Site.Create(RequireTenant(), request.Subdomain);

        context.Sites.Add(site);
        await context.SaveChangesAsync(cancellationToken);

        return Created(
            "/api/v1/site",
            new SiteResponse(site.Id, site.Subdomain, site.Status.ToString(),
                site.IsPublished, site.Pages.Count, 0));
    }

    /// <summary>Adiciona uma seção a uma página.</summary>
    [HttpPost("pages/{pageId:guid}/sections")]
    [RequirePermission(Permission.ManageTenantSettings)]
    [ProducesResponseType<SectionResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> AddSectionAsync(
        Guid pageId,
        [FromBody] AddSectionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var site = await LoadSiteAsync(cancellationToken);
        var page = site.Pages.FirstOrDefault(item => item.Id == pageId)
            ?? throw NotFoundException.For("Página", pageId);

        var section = page.AddSection(request.ComponentType);

        foreach (var property in request.Configuration)
        {
            section.SetProperty(property.Key, property.Value);
        }

        await context.SaveChangesAsync(cancellationToken);

        return Created(
            $"/api/v1/site/pages/{pageId}/sections/{section.Id}",
            new SectionResponse(section.Id, section.ComponentType.ToString(),
                section.SortOrder, section.IsComplete()));
    }

    /// <summary>Gera a prévia do rascunho atual.</summary>
    [HttpGet("preview")]
    [RequirePermission(Permission.ViewTenantSettings)]
    [ProducesResponseType<string>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PreviewAsync(CancellationToken cancellationToken)
    {
        var site = await LoadSiteAsync(cancellationToken);

        return Ok(site.BuildSnapshot());
    }

    /// <summary>Publica o rascunho, gerando uma nova versão.</summary>
    [HttpPost("publish")]
    [RequirePermission(Permission.ManageTenantSettings)]
    [ProducesResponseType<PublicationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PublishAsync(CancellationToken cancellationToken)
    {
        var site = await LoadSiteAsync(cancellationToken);
        var authorId = RequireUserId();

        var publication = site.Publish(authorId, timeProvider.GetUtcNow());

        await context.SaveChangesAsync(cancellationToken);

        return Ok(new PublicationResponse(
            publication.Id, publication.Version, publication.PublishedAt));
    }

    /// <summary>Restaura uma versão anterior.</summary>
    [HttpPost("rollback/{publicationId:guid}")]
    [RequirePermission(Permission.ManageTenantSettings)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RollbackAsync(
        Guid publicationId,
        CancellationToken cancellationToken)
    {
        var site = await LoadSiteAsync(cancellationToken);

        site.RollbackTo(publicationId);
        await context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private async Task<Site> LoadSiteAsync(CancellationToken cancellationToken) =>
        await context.Sites.FirstOrDefaultAsync(cancellationToken)
        ?? throw NotFoundException.For("Site", RequireTenant());

    private Guid RequireTenant() =>
        tenantContext.TenantId ?? throw new DomainRuleException("Estabelecimento não identificado.");

    private Guid RequireUserId()
    {
        var claim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(claim, out var userId)
            ? userId
            : throw new DomainRuleException("Usuário não identificado.");
    }
}

/// <summary>Corpo de criação de site.</summary>
public sealed record CreateSiteRequest([Required, MaxLength(40)] string Subdomain);

/// <summary>Corpo de adição de seção.</summary>
public sealed record AddSectionRequest(
    SiteComponentType ComponentType,
    IReadOnlyDictionary<string, string> Configuration);

/// <summary>Representação de site.</summary>
public sealed record SiteResponse(
    Guid Id, string Subdomain, string Status, bool IsPublished, int PageCount, int PublicationCount);

/// <summary>Representação de seção.</summary>
public sealed record SectionResponse(
    Guid Id, string ComponentType, int SortOrder, bool IsComplete);

/// <summary>Representação de publicação.</summary>
public sealed record PublicationResponse(Guid Id, int Version, DateTimeOffset PublishedAt);
