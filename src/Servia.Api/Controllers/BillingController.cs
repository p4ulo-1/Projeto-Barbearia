using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Servia.Api.Authorization;
using Servia.Api.Contracts;
using Servia.Application.Billing;
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
public sealed class BillingController(
    AppDbContext context,
    ITenantContext tenantContext,
    SubscriptionService subscriptions) : ControllerBase
{
    /// <summary>Consulta a assinatura vigente.</summary>
    [HttpGet("subscription")]
    [RequirePermission(Permission.ViewTenantSettings)]
    [ProducesResponseType<SubscriptionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSubscriptionAsync(CancellationToken cancellationToken)
    {
        // O filtro global ja restringe ao tenant corrente (Subscription
        // implementa ITenantScoped). O filtro explicito documenta a
        // intencao e protege caso o filtro global seja desativado.
        var tenantId = RequireTenant();

        var subscription = await context.Subscriptions
            .Where(item => item.TenantId == tenantId)
            .OrderByDescending(item => item.CurrentPeriodStart)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw NotFoundException.For("Assinatura", tenantId);

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
                quota.Remaining,
                quota.ConsumedTokens))
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

    /// <summary>Contrata um plano para o estabelecimento (F16.1).</summary>
    [HttpPost("subscription")]
    [RequirePermission(Permission.ManageBilling)]
    [ProducesResponseType<SubscriptionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubscribeAsync(
        [FromBody] SubscribePlanRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await subscriptions.SubscribeAsync(
            RequireTenant(), request.PlanCode, RequireUserId(), cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new ErrorResponse(result.FailureReason!));
        }

        return Created("/api/v1/billing/subscription", ToResponse(result));
    }

    /// <summary>Troca o plano da assinatura (F16.2).</summary>
    [HttpPatch("subscription")]
    [RequirePermission(Permission.ManageBilling)]
    [ProducesResponseType<SubscriptionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePlanAsync(
        [FromBody] SubscribePlanRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await subscriptions.ChangePlanAsync(
            RequireTenant(), request.PlanCode, RequireUserId(), cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new ErrorResponse(result.FailureReason!));
        }

        return Ok(ToResponse(result));
    }

    /// <summary>Cancela a assinatura (F16.3).</summary>
    [HttpPost("subscription/cancel")]
    [RequirePermission(Permission.ManageBilling)]
    [ProducesResponseType<SubscriptionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CancelAsync(CancellationToken cancellationToken)
    {
        var result = await subscriptions.CancelAsync(RequireTenant(), cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new ErrorResponse(result.FailureReason!));
        }

        return Ok(ToResponse(result));
    }

    /// <summary>Histórico de mudanças de plano (F16.5).</summary>
    [HttpGet("subscription/history")]
    [RequirePermission(Permission.ViewTenantSettings)]
    [ProducesResponseType<IReadOnlyList<PlanChangeResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistoryAsync(CancellationToken cancellationToken)
    {
        var changes = await context.PlanChanges
            .Where(change => change.TenantId == RequireTenant())
            .OrderByDescending(change => change.ChangedAt)
            .Select(change => new PlanChangeResponse(
                change.Id,
                change.FromPlanId,
                change.ToPlanId,
                change.Reason.ToString(),
                change.ChangedAt))
            .ToListAsync(cancellationToken);

        return Ok(changes);
    }

    /// <summary>Sugere upgrade quando a equipe excede a faixa (F16.4).</summary>
    [HttpGet("subscription/upgrade-suggestion")]
    [RequirePermission(Permission.ViewTenantSettings)]
    [ProducesResponseType<UpgradeSuggestionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetUpgradeSuggestionAsync(CancellationToken cancellationToken)
    {
        var suggestion = await subscriptions.SuggestUpgradeAsync(
            RequireTenant(), cancellationToken);

        if (suggestion is null)
        {
            return NoContent();
        }

        return Ok(new UpgradeSuggestionResponse(
            suggestion.CurrentPlan.Code,
            suggestion.RequiredPlan.Code,
            suggestion.ProfessionalCount));
    }

    private static SubscriptionResponse ToResponse(SubscriptionResult result) =>
        new(
            result.Subscription!.Id,
            result.Plan?.Code ?? "desconhecido",
            result.Plan?.Name ?? "desconhecido",
            result.Subscription.Status.ToString(),
            result.Subscription.CurrentPeriodStart,
            result.Subscription.CurrentPeriodEnd,
            result.Subscription.TrialEndsAt);

    private Guid RequireUserId()
    {
        var claim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(claim, out var userId)
            ? userId
            : throw new DomainRuleException("Usuário não identificado.");
    }

    private Guid RequireTenant() =>
        tenantContext.TenantId ?? throw new DomainRuleException("Estabelecimento não identificado.");
}

/// <summary>Corpo de contratação ou troca de plano.</summary>
public sealed record SubscribePlanRequest(
    [Required, MaxLength(30)] string PlanCode);

/// <summary>Representação de mudança de plano.</summary>
public sealed record PlanChangeResponse(
    Guid Id, Guid? FromPlanId, Guid ToPlanId, string Reason, DateTimeOffset ChangedAt);

/// <summary>Sugestão de upgrade por excedente de profissionais.</summary>
public sealed record UpgradeSuggestionResponse(
    string CurrentPlanCode, string RequiredPlanCode, int ProfessionalCount);

/// <summary>Representação de assinatura.</summary>
public sealed record SubscriptionResponse(
    Guid Id, string PlanCode, string PlanName, string Status,
    DateOnly PeriodStart, DateOnly PeriodEnd, DateOnly? TrialEndsAt);

/// <summary>Representação de fatura.</summary>
public sealed record InvoiceResponse(
    Guid Id, DateOnly PeriodStart, DateOnly PeriodEnd,
    DateOnly DueDate, string Status, decimal Total);

/// <summary>
/// Representação de consumo. Para IA, <c>Consumed</c> é em créditos e
/// <c>ConsumedTokens</c> é o total real de tokens (entrada + saída).
/// </summary>
public sealed record UsageResponse(
    string Resource,
    int Included,
    int Consumed,
    int PurchasedExtra,
    int Remaining,
    long ConsumedTokens);

/// <summary>Representação de plano.</summary>
public sealed record PlanResponse(
    string Code, string Name, decimal MonthlyPrice,
    int IncludedProfessionals, string CustomizationLevel, int IncludedAiUnits);
