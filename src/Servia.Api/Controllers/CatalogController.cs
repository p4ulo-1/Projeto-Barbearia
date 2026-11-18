using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Servia.Api.Authorization;
using Servia.Api.Contracts;
using Servia.Application.Billing;
using Servia.Domain.Catalog;
using Servia.Domain.Common;
using Servia.Domain.Identity;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;

namespace Servia.Api.Controllers;

/// <summary>
/// Endpoints de catálogo: serviços, categorias, variações e combos (F13.1).
///
/// O controller é fino: valida a forma do pedido, delega ao domínio e
/// traduz o resultado em resposta HTTP. A regra de negócio vive nas
/// entidades, não aqui.
/// </summary>
[ApiController]
[Route("api/v1/catalog")]
[Authorize]
public sealed class CatalogController(
    AppDbContext context,
    ITenantContext tenantContext,
    PlanSlotService planSlots,
    ISubscriptionRepository subscriptions) : ControllerBase
{
    // ---------- Serviços ----------

    /// <summary>Lista os serviços do estabelecimento, com paginação.</summary>
    [HttpGet("services")]
    [RequirePermission(Permission.ViewServices)]
    [ProducesResponseType<PagedResponse<ServiceResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListServicesAsync(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromQuery] bool? onlyActive,
        CancellationToken cancellationToken)
    {
        var request = PageRequest.Normalize(page, pageSize);
        var query = context.Services.AsQueryable();

        if (onlyActive == true)
        {
            query = query.Where(service => service.IsActive);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(service => service.Name)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(service => ServiceResponse.From(service))
            .ToListAsync(cancellationToken);

        return Ok(PagedResponse<ServiceResponse>.Create(items, request, total));
    }

    /// <summary>Consulta um serviço pelo identificador.</summary>
    [HttpGet("services/{id:guid}")]
    [RequirePermission(Permission.ViewServices)]
    [ProducesResponseType<ServiceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetServiceAsync(Guid id, CancellationToken cancellationToken) =>
        Ok(ServiceResponse.From(await FindServiceAsync(id, cancellationToken)));

    /// <summary>Cria um serviço.</summary>
    [HttpPost("services")]
    [RequirePermission(Permission.ManageServices)]
    [ProducesResponseType<ServiceResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateServiceAsync(
        [FromBody] CreateServiceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tenantId = RequireTenant();
        await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken);

        var service = Service.Create(
            tenantId,
            request.Name,
            Money.From(request.Price),
            request.DurationMinutes,
            request.Description,
            request.CategoryId,
            request.Highlight);

        context.Services.Add(service);
        await context.SaveChangesAsync(cancellationToken);

        return Created($"/api/v1/catalog/services/{service.Id}", ServiceResponse.From(service));
    }

    /// <summary>Atualiza os dados cadastrais e a duração de um serviço.</summary>
    [HttpPut("services/{id:guid}")]
    [RequirePermission(Permission.ManageServices)]
    [ProducesResponseType<ServiceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateServiceAsync(
        Guid id,
        [FromBody] UpdateServiceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var service = await planSlots.ExecuteWithSlotCheckAsync(RequireTenant(), async token =>
        {
            var item = await FindServiceAsync(id, token);
            await EnsureCategoryExistsAsync(request.CategoryId, token);
            item.Update(request.Name, request.Description, request.CategoryId, request.Highlight);
            item.ChangeDuration(request.DurationMinutes);
            await context.SaveChangesAsync(token);
            return item;
        }, cancellationToken);

        return Ok(ServiceResponse.From(service));
    }

    /// <summary>Altera o preço de um serviço.</summary>
    [HttpPatch("services/{id:guid}/price")]
    [RequirePermission(Permission.ManageServices)]
    [ProducesResponseType<ServiceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeServicePriceAsync(
        Guid id,
        [FromBody] ChangePriceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var price = Money.From(request.Price);
        // Corrigir um preço legado inválido pode habilitar profissionais já vinculados.
        var service = await planSlots.ExecuteWithSlotCheckAsync(RequireTenant(), async token =>
        {
            var item = await FindServiceAsync(id, token);
            item.ChangePrice(price);
            await context.SaveChangesAsync(token);
            return item;
        }, cancellationToken);

        return Ok(ServiceResponse.From(service));
    }

    /// <summary>Reativa um serviço e verifica a ocupação dos profissionais vinculados.</summary>
    [HttpPost("services/{id:guid}/activate")]
    [RequirePermission(Permission.ManageServices)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActivateServiceAsync(Guid id, CancellationToken cancellationToken)
    {
        await planSlots.ExecuteWithSlotCheckAsync(RequireTenant(), async token =>
        {
            var service = await FindServiceAsync(id, token);
            service.Activate();
            await context.SaveChangesAsync(token);
            return true;
        }, cancellationToken);

        return NoContent();
    }

    /// <summary>Desativa um serviço, sem removê-lo do histórico.</summary>
    [HttpPost("services/{id:guid}/deactivate")]
    [RequirePermission(Permission.ManageServices)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeactivateServiceAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        // Desativação só pode reduzir ocupação, mas participa do isolamento dos demais escritores.
        await subscriptions.ExecuteSerializableAsync(async token =>
        {
            var service = await FindServiceAsync(id, token);
            service.Deactivate();
            await context.SaveChangesAsync(token);
            return true;
        }, cancellationToken);

        return NoContent();
    }

    // ---------- Categorias ----------

    /// <summary>Lista as categorias do estabelecimento.</summary>
    [HttpGet("categories")]
    [RequirePermission(Permission.ViewServices)]
    [ProducesResponseType<IReadOnlyList<CategoryResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListCategoriesAsync(CancellationToken cancellationToken)
    {
        var items = await context.ServiceCategories
            .OrderBy(category => category.SortOrder)
            .Select(category => new CategoryResponse(category.Id, category.Name, category.SortOrder))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    /// <summary>Cria uma categoria.</summary>
    [HttpPost("categories")]
    [RequirePermission(Permission.ManageServices)]
    [ProducesResponseType<CategoryResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateCategoryAsync(
        [FromBody] CreateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var category = ServiceCategory.Create(RequireTenant(), request.Name, request.SortOrder);

        context.ServiceCategories.Add(category);
        await context.SaveChangesAsync(cancellationToken);

        return Created(
            $"/api/v1/catalog/categories/{category.Id}",
            new CategoryResponse(category.Id, category.Name, category.SortOrder));
    }

    // ---------- Combos ----------

    /// <summary>Lista os combos do estabelecimento.</summary>
    [HttpGet("combos")]
    [RequirePermission(Permission.ViewServices)]
    [ProducesResponseType<IReadOnlyList<ComboResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListCombosAsync(CancellationToken cancellationToken)
    {
        var items = await context.ServiceCombos
            .Select(combo => new ComboResponse(
                combo.Id,
                combo.Name,
                combo.ComboPrice.Amount,
                combo.Items.Count))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    /// <summary>Cria um combo.</summary>
    [HttpPost("combos")]
    [RequirePermission(Permission.ManageServices)]
    [ProducesResponseType<ComboResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateComboAsync(
        [FromBody] CreateComboRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var combo = ServiceCombo.Create(RequireTenant(), request.Name, Money.From(request.Price));

        foreach (var item in request.Items)
        {
            combo.AddItem(item.ServiceId, item.Quantity);
        }

        context.ServiceCombos.Add(combo);
        await context.SaveChangesAsync(cancellationToken);

        return Created(
            $"/api/v1/catalog/combos/{combo.Id}",
            new ComboResponse(combo.Id, combo.Name, combo.ComboPrice.Amount, combo.Items.Count));
    }

    private async Task<Service> FindServiceAsync(Guid id, CancellationToken cancellationToken) =>
        await context.Services.FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
        ?? throw NotFoundException.For("Serviço", id);

    private async Task EnsureCategoryExistsAsync(Guid? categoryId, CancellationToken cancellationToken)
    {
        if (categoryId is { } id
            && !await context.ServiceCategories.AnyAsync(item => item.Id == id, cancellationToken))
        {
            throw NotFoundException.For("Categoria", id);
        }
    }

    private Guid RequireTenant() =>
        tenantContext.TenantId ?? throw new DomainRuleException("Estabelecimento não identificado.");
}

// ---------- Contratos ----------

/// <summary>Corpo de criação de serviço.</summary>
public sealed record CreateServiceRequest(
    [Required, MaxLength(150)] string Name,
    [property: JsonRequired] [Range(0, 1_000_000)] decimal Price,
    [Range(1, 1440)] int DurationMinutes,
    [MaxLength(1000)] string? Description = null,
    Guid? CategoryId = null,
    bool Highlight = false);

/// <summary>Dados do serviço editáveis separadamente do preço e do estado.</summary>
public sealed record UpdateServiceRequest(
    [Required, MaxLength(150)] string Name,
    [Range(1, 1440)] int DurationMinutes,
    [MaxLength(1000)] string? Description = null,
    Guid? CategoryId = null,
    bool Highlight = false);

/// <summary>Corpo de alteração de preço.</summary>
public sealed record ChangePriceRequest([property: JsonRequired] [Range(0, 1_000_000)] decimal Price);

/// <summary>Corpo de criação de categoria.</summary>
public sealed record CreateCategoryRequest(
    [Required, MaxLength(100)] string Name,
    [Range(0, 1000)] int SortOrder = 0);

/// <summary>Corpo de criação de combo.</summary>
public sealed record CreateComboRequest(
    [Required, MaxLength(150)] string Name,
    [Range(0.01, 1_000_000)] decimal Price,
    [Required, MinLength(1)] IReadOnlyList<ComboItemRequest> Items);

/// <summary>Item de um combo.</summary>
public sealed record ComboItemRequest(Guid ServiceId, [Range(1, 100)] int Quantity = 1);

/// <summary>Representação de serviço.</summary>
public sealed record ServiceResponse(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    int DurationMinutes,
    Guid? CategoryId,
    bool Highlight,
    bool IsActive)
{
    public static ServiceResponse From(Service service) =>
        new(service.Id, service.Name, service.Description, service.Price.Amount,
            service.DurationMinutes, service.CategoryId, service.Highlight, service.IsActive);
}

/// <summary>Representação de categoria.</summary>
public sealed record CategoryResponse(Guid Id, string Name, int SortOrder);

/// <summary>Representação de combo.</summary>
public sealed record ComboResponse(Guid Id, string Name, decimal Price, int ItemCount);
