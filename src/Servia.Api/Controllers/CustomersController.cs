using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Servia.Api.Authorization;
using Servia.Api.Contracts;
using Servia.Domain.Common;
using Servia.Domain.Customers;
using Servia.Domain.Identity;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;

namespace Servia.Api.Controllers;

/// <summary>
/// Endpoints de clientes e preferências (F13.3).
/// </summary>
[ApiController]
[Route("api/v1/customers")]
[Authorize]
public sealed class CustomersController(
    AppDbContext context,
    ITenantContext tenantContext,
    TimeProvider timeProvider) : ControllerBase
{
    /// <summary>Lista os clientes, com paginação e busca por nome ou telefone.</summary>
    [HttpGet]
    [RequirePermission(Permission.ViewCustomers)]
    [ProducesResponseType<PagedResponse<CustomerResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var request = PageRequest.Normalize(page, pageSize);
        var query = context.Customers.Where(customer => customer.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            // R3: busca case-insensitive (Etapa 7).
            // O PostgreSQL e case-sensitive em LIKE; o SQLite era
            // case-insensitive para ASCII. Usar ILIKE mantem o
            // comportamento esperado pelo usuario, independente do banco.
            var term = search.Trim();

            query = query.Where(customer =>
                EF.Functions.ILike(customer.Name, $"%{term}%")
                || EF.Functions.ILike(customer.Phone, $"%{term}%"));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(customer => customer.Name)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(customer => new CustomerResponse(
                customer.Id, customer.Name, customer.Phone, customer.Email, customer.IsActive))
            .ToListAsync(cancellationToken);

        return Ok(PagedResponse<CustomerResponse>.Create(items, request, total));
    }

    /// <summary>Busca um cliente pelo identificador.</summary>
    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.ViewCustomers)]
    [ProducesResponseType<CustomerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var customer = await context.Customers
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (customer is null)
        {
            throw NotFoundException.For("Cliente", id);
        }

        return Ok(new CustomerResponse(
            customer.Id, customer.Name, customer.Phone, customer.Email, customer.IsActive));
    }

    /// <summary>Cadastra um cliente.</summary>
    [HttpPost]
    [RequirePermission(Permission.ManageCustomers)]
    [ProducesResponseType<CustomerResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var customer = Customer.Create(
            RequireTenant(),
            request.Name,
            request.Phone,
            request.Email,
            request.BirthDate,
            now: timeProvider.GetUtcNow());

        context.Customers.Add(customer);
        await context.SaveChangesAsync(cancellationToken);

        return Created(
            $"/api/v1/customers/{customer.Id}",
            new CustomerResponse(
                customer.Id, customer.Name, customer.Phone, customer.Email, customer.IsActive));
    }

    /// <summary>
    /// Anonimiza os dados pessoais do cliente (LGPD).
    /// O histórico financeiro é preservado.
    /// </summary>
    [HttpPost("{id:guid}/anonymize")]
    [RequirePermission(Permission.ManageCustomers)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AnonymizeAsync(Guid id, CancellationToken cancellationToken)
    {
        var customer = await context.Customers
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (customer is null)
        {
            throw NotFoundException.For("Cliente", id);
        }

        customer.Anonymize();
        await context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>Consulta as preferências do cliente.</summary>
    [HttpGet("{id:guid}/preferences")]
    [RequirePermission(Permission.ViewCustomers)]
    [ProducesResponseType<PreferenceResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPreferencesAsync(Guid id, CancellationToken cancellationToken)
    {
        var preference = await context.CustomerPreferences
            .FirstOrDefaultAsync(item => item.CustomerId == id, cancellationToken);

        return Ok(preference is null
            ? new PreferenceResponse(id, null, null)
            : new PreferenceResponse(id, preference.PreferredProfessionalId, preference.Notes));
    }

    /// <summary>Define as preferências do cliente.</summary>
    [HttpPut("{id:guid}/preferences")]
    [RequirePermission(Permission.ManageCustomers)]
    [ProducesResponseType<PreferenceResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetPreferencesAsync(
        Guid id,
        [FromBody] SetPreferenceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var preference = await context.CustomerPreferences
            .FirstOrDefaultAsync(item => item.CustomerId == id, cancellationToken);

        if (preference is null)
        {
            preference = CustomerPreference.Create(
                RequireTenant(), id, request.PreferredProfessionalId, request.Notes);

            context.CustomerPreferences.Add(preference);
        }
        else
        {
            preference.SetPreferredProfessional(request.PreferredProfessionalId);
            preference.UpdateNotes(request.Notes);
        }

        await context.SaveChangesAsync(cancellationToken);

        return Ok(new PreferenceResponse(
            id, preference.PreferredProfessionalId, preference.Notes));
    }

    private Guid RequireTenant() =>
        tenantContext.TenantId ?? throw new DomainRuleException("Estabelecimento não identificado.");
}

/// <summary>Corpo de cadastro de cliente.</summary>
public sealed record CreateCustomerRequest(
    [Required, MaxLength(150)] string Name,
    [Required, MaxLength(20)] string Phone,
    [EmailAddress, MaxLength(200)] string? Email = null,
    DateOnly? BirthDate = null);

/// <summary>Corpo de definição de preferências.</summary>
public sealed record SetPreferenceRequest(
    Guid? PreferredProfessionalId = null,
    [MaxLength(1000)] string? Notes = null);

/// <summary>Representação de cliente.</summary>
public sealed record CustomerResponse(
    Guid Id, string Name, string Phone, string? Email, bool IsActive);

/// <summary>Representação de preferências.</summary>
public sealed record PreferenceResponse(
    Guid CustomerId, Guid? PreferredProfessionalId, string? Notes);
