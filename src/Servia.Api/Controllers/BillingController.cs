using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Servia.Api.Authorization;
using Servia.Api.Contracts;
using Servia.Domain.Billing;
using Servia.Domain.Common;
using Servia.Domain.Identity;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;

namespace Servia.Api.Controllers;

/// <summary>
/// Endpoints de billing da plataforma (F13.6).
///
/// O tenant consulta a própria assinatura, as faturas e o consumo. Não há
/// acesso a dados de outro estabelecimento: o filtro global garante isso.
/// </summary>
[ApiController]
[Route("api/v1/billing")]
[Authorize]
public sealed class BillingController(AppDbContext context, ITenantContext tenantContext) : ControllerBase
{
    /// <summary>Consulta a assinatura vigente.</summary>
    [HttpGet("subscription")]
    [RequirePermission(Permission.ViewTenantSettings)]
    [ProducesResponseType<SubscriptionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSubscriptionAsync(CancellationToken cancellationToken)
    {
        var subscription = await context.Subscriptions
            .OrderByDescending(item => item.CurrentPeriodStart)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw NotFoundException.For("Assinatura", RequireTenant());

        var plan = await context.Plans
            .FirstOrDefaultAsync(item => item.Id == subscription.PlanId, cancellationToken);

        return Ok(new SubscriptionResponse(
            subscription.Id,
            plan?.Code ?? "desconhecido",
            plan?.Name ?? "desconhecido",
            subscription.Status.ToString(),
            subscription.CurrentPeriodStart,
            subscription.CurrentPeriodEnd,
            subscription.TrialEndsAt));
    }

    /// <summary>Lista as faturas da plataforma, com paginação.</summary>
    [HttpGet("invoices")]
    [RequirePermission(Permission.ViewTenantSettings)]
    [ProducesResponseType<PagedResponse<InvoiceResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListInvoicesAsync(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        var request = PageRequest.Normalize(page, pageSize);
        var query = context.PlatformInvoices.AsQueryable();

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(invoice => invoice.PeriodStart)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(invoice => new InvoiceResponse(
                invoice.Id,
                invoice.PeriodStart,
                invoice.PeriodEnd,
                invoice.DueDate,
                invoice.Status.ToString(),
                invoice.Total.Amount))
            .ToListAsync(cancellationToken);

        return Ok(PagedResponse<InvoiceResponse>.Create(items, request, total));
    }

    /// <summary>Consulta o consumo do período por recurso.</summary>
    [HttpGet("usage")]
    [RequirePermission(Permission.ViewTenantSettings)]
    [ProducesResponseType<IReadOnlyList<UsageResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsageAsync(CancellationToken cancellationToken)
    {
        var items = await context.UsageQuotas
            .OrderBy(quota => quota.Resource)
            .Select(quota => new UsageResponse(
                quota.Resource.ToString(),
                quota.Included,
                quota.Consumed,
                quota.PurchasedExtra,
                quota.Remaining))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    /// <summary>Lista os planos disponíveis para contratação.</summary>
    [HttpGet("plans")]
    [AllowAnonymous]
    [ProducesResponseType<IReadOnlyList<PlanResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPlansAsync(CancellationToken cancellationToken)
    {
        var plans = await context.Plans
            .Where(plan => plan.IsActive)
            .OrderBy(plan => plan.MonthlyPrice.Amount)
            .Select(plan => new PlanResponse(
                plan.Code,
                plan.Name,
                plan.MonthlyPrice.Amount,
                plan.IncludedProfessionals,
                plan.CustomizationLevel.ToString(),
                plan.IncludedAiUnits))
            .ToListAsync(cancellationToken);

        return Ok(plans);
    }

    private Guid RequireTenant() =>
        tenantContext.TenantId ?? throw new DomainRuleException("Estabelecimento não identificado.");
}

/// <summary>Representação de assinatura.</summary>
public sealed record SubscriptionResponse(
    Guid Id, string PlanCode, string PlanName, string Status,
    DateOnly PeriodStart, DateOnly PeriodEnd, DateOnly? TrialEndsAt);

/// <summary>Representação de fatura.</summary>
public sealed record InvoiceResponse(
    Guid Id, DateOnly PeriodStart, DateOnly PeriodEnd,
    DateOnly DueDate, string Status, decimal Total);

/// <summary>Representação de consumo.</summary>
public sealed record UsageResponse(
    string Resource, int Included, int Consumed, int PurchasedExtra, int Remaining);

/// <summary>Representação de plano.</summary>
public sealed record PlanResponse(
    string Code, string Name, decimal MonthlyPrice,
    int IncludedProfessionals, string CustomizationLevel, int IncludedAiUnits);
