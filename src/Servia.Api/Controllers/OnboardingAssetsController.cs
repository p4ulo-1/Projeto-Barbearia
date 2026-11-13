using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Servia.Api.Authorization;
using Servia.Api.Contracts;
using Servia.Application.Sites;
using Servia.Domain.Common;
using Servia.Domain.Identity;
using Servia.Domain.Sites;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;

namespace Servia.Api.Controllers;

/// <summary>
/// Recebimento e gestão dos arquivos do site (item 1.3).
///
/// <b>Não é informação validada por ser enviada:</b> todo arquivo entra
/// pendente e só é usado após aprovação explícita.
///
/// <b>Isolamento:</b> o arquivo é gravado sob o tenant e consultado pelo
/// contexto. Um estabelecimento não acessa o arquivo de outro.
///
/// <b>Download seguro:</b> o conteúdo é servido com
/// <c>Content-Disposition: attachment</c> e <c>nosniff</c>, para que um SVG
/// ou arquivo adulterado não execute no navegador.
/// </summary>
[ApiController]
[Route("api/v1/onboarding/sessions/{sessionId:guid}/assets")]
[Authorize]
public sealed class OnboardingAssetsController(
    SiteAssetService assetService,
    AppDbContext context,
    ITenantContext tenantContext) : ControllerBase
{
    private const long MaxUploadBytes = SiteAsset.MaxSizeBytes;

    /// <summary>Lista os arquivos da sessão.</summary>
    [HttpGet]
    [RequirePermission(Permission.ViewTenantSettings)]
    [ProducesResponseType<IReadOnlyList<AssetResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();

        var assets = await context.SiteAssets
            .Where(asset => asset.OnboardingSessionId == sessionId)
            .OrderByDescending(asset => asset.UploadedAt)
            .ToListAsync(cancellationToken);

        return Ok(assets.Select(AssetResponse.From).ToList());
    }

    /// <summary>Recebe um arquivo.</summary>
    [HttpPost]
    [RequirePermission(Permission.ManageTenantSettings)]
    [RequestSizeLimit(MaxUploadBytes)]
    [ProducesResponseType<AssetResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadAsync(
        Guid sessionId,
        [FromForm] SiteAssetKind kind,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);

        var tenantId = RequireTenant();

        // C5: o arquivo so pode ser associado a uma sessao do proprio tenant.
        // A verificacao fica aqui (o controller ja conhece a sessao); o
        // SiteAssetService cuida apenas do arquivo (C11).
        var session = await context.OnboardingSessions
            .FirstOrDefaultAsync(item => item.Id == sessionId && item.TenantId == tenantId, cancellationToken);

        if (session is null)
        {
            throw NotFoundException.For("Sessão de onboarding", sessionId);
        }

        await using var stream = file.OpenReadStream();

        var asset = await assetService.UploadAsync(
            tenantId,
            sessionId,
            kind,
            file.FileName,
            file.ContentType,
            stream,
            cancellationToken);

        return Created(
            $"/api/v1/onboarding/sessions/{sessionId}/assets/{asset.Id}",
            AssetResponse.From(asset));
    }

    /// <summary>Aprova um arquivo para uso na configuração.</summary>
    [HttpPost("{assetId:guid}/approve")]
    [RequirePermission(Permission.ManageTenantSettings)]
    [ProducesResponseType<AssetResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveAsync(
        Guid sessionId,
        Guid assetId,
        CancellationToken cancellationToken)
    {
        var asset = await LoadAsync(sessionId, assetId, cancellationToken);

        asset.Approve();
        await context.SaveChangesAsync(cancellationToken);

        return Ok(AssetResponse.From(asset));
    }

    /// <summary>Rejeita um arquivo, com motivo.</summary>
    [HttpPost("{assetId:guid}/reject")]
    [RequirePermission(Permission.ManageTenantSettings)]
    [ProducesResponseType<AssetResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectAsync(
        Guid sessionId,
        Guid assetId,
        [FromBody] RejectAssetRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var asset = await LoadAsync(sessionId, assetId, cancellationToken);

        asset.Reject(request.Reason);
        await context.SaveChangesAsync(cancellationToken);

        return Ok(AssetResponse.From(asset));
    }

    /// <summary>Baixa o arquivo, sempre escopado ao tenant.</summary>
    [HttpGet("{assetId:guid}/content")]
    [RequirePermission(Permission.ViewTenantSettings)]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadAsync(
        Guid sessionId,
        Guid assetId,
        CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();
        var asset = await LoadAsync(sessionId, assetId, cancellationToken);

        await using var stream = await assetService.OpenAsync(tenantId, asset, cancellationToken);

        // C8: o nome vem do cliente. Sanitiza antes de ir ao cabecalho para
        // impedir injecao (aspas, CR/LF). O FileResult define o
        // Content-Disposition como attachment; nao o definimos duas vezes.
        var safeName = SanitizeFileName(asset.FileName);

        Response.Headers.XContentTypeOptions = "nosniff";

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);

        return File(buffer.ToArray(), asset.ContentType, safeName);
    }

    /// <summary>
    /// Remove do nome de arquivo caracteres que quebrariam o cabecalho
    /// Content-Disposition (C8): aspas, CR/LF e caracteres de controle.
    /// </summary>
    internal static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "arquivo";
        }

        var builder = new System.Text.StringBuilder(fileName.Length);

        foreach (var character in fileName)
        {
            if (character is '"' or '\\' || char.IsControl(character))
            {
                continue;
            }

            builder.Append(character);
        }

        var sanitized = builder.ToString().Trim();

        return string.IsNullOrWhiteSpace(sanitized) ? "arquivo" : sanitized;
    }

    private async Task<SiteAsset> LoadAsync(
        Guid sessionId,
        Guid assetId,
        CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();

        return await context.SiteAssets
            .FirstOrDefaultAsync(asset => asset.Id == assetId
                && asset.OnboardingSessionId == sessionId
                && asset.TenantId == tenantId, cancellationToken)
            ?? throw NotFoundException.For("Arquivo", assetId);
    }

    private Guid RequireTenant() =>
        tenantContext.TenantId
            ?? throw new DomainRuleException("Estabelecimento não identificado.");
}

/// <summary>Representação de arquivo.</summary>
public sealed record AssetResponse(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeInBytes,
    string Kind,
    string Status,
    bool IsUsable,
    DateTimeOffset UploadedAt)
{
    public static AssetResponse From(SiteAsset asset) =>
        new(asset.Id, asset.FileName, asset.ContentType, asset.SizeInBytes,
            asset.Kind.ToString(), asset.Status.ToString(), asset.IsUsable, asset.UploadedAt);
}

/// <summary>Corpo de rejeição de arquivo.</summary>
public sealed record RejectAssetRequest(
    [System.ComponentModel.DataAnnotations.Required,
     System.ComponentModel.DataAnnotations.MaxLength(300)] string Reason);
