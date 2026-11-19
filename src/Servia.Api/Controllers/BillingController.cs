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
    SubscriptionService subscriptions,
    SubscriptionCheckoutService checkout,
    SubscriptionLifecycleService lifecycle,
    SubscriptionPlanChangeService planChanges,
    SubscriptionAccessService subscriptionAccess,
    TimeProvider clock) : ControllerBase
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

        var subscription = await subscriptions.FindCurrentAsync(tenantId, cancellationToken)
            ?? throw NotFoundException.For("Assinatura", tenantId);

        var plan = await context.Plans
            .FirstOrDefaultAsync(item => item.Id == subscription.EffectivePlanId, cancellationToken);

        string? firstPaymentStatus = null;
        if (subscription.TrialStartedAtUtc.HasValue && subscription.TrialEndsAtUtc is { } firstDue
            && (subscription.Status != SubscriptionStatus.Cancelled || subscription.FirstPaymentId is not null))
        {
            if (subscription.FirstPaymentId is not null)
            {
                firstPaymentStatus = "Paid";
            }
            else
            {
                var invoiceStatus = await context.PlatformInvoices.AsNoTracking()
                    .Where(invoice => invoice.TenantId == tenantId && invoice.SubscriptionId == subscription.Id
                        && invoice.RecurringDueAtUtc == firstDue)
                    .Select(invoice => (InvoiceStatus?)invoice.Status)
                    .FirstOrDefaultAsync(cancellationToken);
                firstPaymentStatus = invoiceStatus == InvoiceStatus.Failed ? "Rejected"
                    : clock.GetUtcNow() < firstDue ? "Scheduled" : "Pending";
            }
        }

        return Ok(new SubscriptionResponse(
            subscription.Id,
            plan?.Code ?? "desconhecido",
            plan?.Name ?? "desconhecido",
            subscription.Status.ToString(),
            subscription.CurrentPeriodStart,
            subscription.CurrentPeriodEnd,
            subscription.TrialEndsAt, subscription.TrialStartedAtUtc, subscription.TrialDeadline,
            subscription.ChargeDeadline, subscription.CancellationDeadline,
            subscription.PastDueSince, subscription.CancellationRequestedOn.HasValue,
            (await subscriptionAccess.GetAsync(tenantId, cancellationToken)).ToString(), firstPaymentStatus));
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

    /// <summary>Contratação indisponível até habilitar confirmação financeira.</summary>
    [HttpPost("subscription")]
    [RequirePermission(Permission.ManageBilling)]
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

    [HttpPost("subscription/checkout")]
    [RequirePermission(Permission.ManageBilling)]
    [ProducesResponseType<CheckoutResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<CheckoutResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> StartCheckoutAsync([FromBody] StartCheckoutRequest request, CancellationToken cancellationToken)
    {
        if (!checkout.CanStartCheckout)
        {
            return CheckoutUnavailable();
        }
        var result = await checkout.StartAsync(RequireTenant(), RequireUserId(), request.CheckoutKey,
            request.PlanCode, cancellationToken, request.CardToken);
        var response = CheckoutResponse.From(result);
        return result.State is "Active" or "Failed" or "Trialing" ? Ok(response) : Accepted(response);
    }

    [HttpGet("subscription/checkout/current")]
    [RequirePermission(Permission.ManageBilling)]
    [ProducesResponseType<CheckoutResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetCurrentCheckoutAsync(CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();
        var pending = await context.Subscriptions.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.CheckoutKey != null
                && (item.Status == SubscriptionStatus.PendingCheckout
                    || item.Status == SubscriptionStatus.PendingConfirmation
                    || item.Status == SubscriptionStatus.AuthorizedAwaitingFirstPayment))
            .Select(item => new { item.CheckoutKey, item.PlanId })
            .FirstOrDefaultAsync(cancellationToken);
        if (pending?.CheckoutKey is not { } key) return NoContent();
        var planCode = await context.Plans.AsNoTracking().Where(plan => plan.Id == pending.PlanId)
            .Select(plan => plan.Code).FirstOrDefaultAsync(cancellationToken);
        return Ok(CheckoutResponse.From(await checkout.GetAsync(tenantId, key, cancellationToken)) with { PlanCode = planCode });
    }

    [HttpGet("subscription/checkout/{key:guid}")]
    [RequirePermission(Permission.ManageBilling)]
    [ProducesResponseType<CheckoutResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCheckoutAsync(Guid key, CancellationToken cancellationToken) =>
        Ok(CheckoutResponse.From(await checkout.GetAsync(RequireTenant(), key, cancellationToken)));

    [HttpPost("subscription/checkout/{key:guid}/verify")]
    [RequirePermission(Permission.ManageBilling)]
    [ProducesResponseType<CheckoutResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> VerifyCheckoutAsync(Guid key, CancellationToken cancellationToken)
    {
        if (!checkout.IsConfigured)
        {
            return CheckoutUnavailable();
        }
        return Ok(CheckoutResponse.From(await checkout.VerifyAsync(RequireTenant(), RequireUserId(), key, cancellationToken)));
    }

    private ObjectResult CheckoutUnavailable() => StatusCode(StatusCodes.Status503ServiceUnavailable,
        new ErrorResponse("A contratação recorrente está temporariamente indisponível."));

    /// <summary>Troca de plano indisponível até habilitar confirmação financeira.</summary>
    [HttpPatch("subscription")]
    [RequirePermission(Permission.ManageBilling)]
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

    /// <summary>Solicita encerramento da recorrência.</summary>
    [HttpPost("subscription/cancel")]
    [RequirePermission(Permission.ManageBilling)]
    [ProducesResponseType<LifecycleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<LifecycleResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelAsync(CancellationToken cancellationToken)
    {
        var result = await lifecycle.CancelAsync(RequireTenant(), cancellationToken);
        var response = LifecycleResponse.From(result);
        return result.State is "Cancelled" or "CancellationScheduled" ? Ok(response) : Accepted(response);
    }

    [HttpPost("subscription/verify")]
    [RequirePermission(Permission.ManageBilling)]
    [ProducesResponseType<LifecycleResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifySubscriptionAsync(CancellationToken cancellationToken)
    {
        var tenant = RequireTenant();
        var current = await subscriptions.FindCurrentAsync(tenant, cancellationToken);
        if (current is not null) await planChanges.SynchronizeAsync(tenant, current.Id, cancellationToken);
        return Ok(LifecycleResponse.From(await lifecycle.VerifyAsync(tenant, cancellationToken)));
    }

    [HttpPost("subscription/plan-change")]
    [RequirePermission(Permission.ManageBilling)]
    [ProducesResponseType<PlanChangeRequestResponse>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> RequestPlanChangeAsync([FromBody] RequestPlanChange request,
        CancellationToken cancellationToken)
    {
        var change = await planChanges.RequestAsync(RequireTenant(), RequireUserId(), request.PlanCode,
            request.OperationKey ?? Guid.NewGuid(), cancellationToken);
        return Accepted(await DescribeChangeAsync(change, cancellationToken));
    }

    [HttpGet("subscription/plan-change")]
    [RequirePermission(Permission.ManageBilling)]
    public async Task<IActionResult> GetPlanChangeAsync(CancellationToken cancellationToken)
    {
        var change = await planChanges.GetLatestAsync(RequireTenant(), cancellationToken);
        return change is null ? NoContent() : Ok(await DescribeChangeAsync(change, cancellationToken));
    }

    [HttpPost("subscription/plan-change/{id:guid}/payment")]
    [RequirePermission(Permission.ManageBilling)]
    public async Task<IActionResult> ConfirmPlanChangePaymentAsync(Guid id, [FromBody] ConfirmPlanChangePayment request,
        CancellationToken cancellationToken) =>
        Accepted(await DescribeChangeAsync(await planChanges.ConfirmPaymentAsync(RequireTenant(), RequireUserId(),
            id, request.CardToken, request.PaymentMethod, cancellationToken), cancellationToken));

    private async Task<PlanChangeRequestResponse> DescribeChangeAsync(PlanChange change, CancellationToken ct) =>
        PlanChangeRequestResponse.From(change) with
        {
            TargetPlanCode = await context.Plans.Where(plan => plan.Id == change.ToPlanId).Select(plan => plan.Code).SingleAsync(ct)
        };

    /// <summary>Histórico de mudanças de plano (F16.5).</summary>
    [HttpGet("subscription/history")]
    [RequirePermission(Permission.ViewTenantSettings)]
    [ProducesResponseType<IReadOnlyList<PlanChangeResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistoryAsync(CancellationToken cancellationToken)
    {
        var changes = await context.PlanChanges
            .Where(change => change.TenantId == RequireTenant() && change.Status == PlanChangeStatus.Effective)
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

public sealed record StartCheckoutRequest(Guid CheckoutKey, [Required, MaxLength(30)] string PlanCode,
    [MaxLength(200)] string? CardToken = null);

public sealed record CheckoutResponse(Guid CheckoutKey, Guid AttemptId, string State,
    DateTimeOffset? TrialStartsAt = null, DateTimeOffset? TrialEndsAt = null,
    string? PlanCode = null)
{
    public static CheckoutResponse From(CheckoutResult result) =>
        new(result.Attempt.CheckoutKey!.Value, result.Attempt.Id, result.State,
            result.Attempt.TrialStartedAtUtc, result.Attempt.TrialDeadline);
}

public sealed record LifecycleResponse(Guid SubscriptionId, string State, string Status,
    DateTimeOffset? NextChargeAt, DateTimeOffset? PastDueSince, DateTimeOffset? CancellationEffectiveAt)
{
    public static LifecycleResponse From(LifecycleResult result) => new(result.Subscription.Id, result.State,
        result.Subscription.Status.ToString(), result.Subscription.ChargeDeadline, result.Subscription.PastDueSince,
        result.Subscription.CancellationDeadline);
}

public sealed record RequestPlanChange([Required, MaxLength(30)] string PlanCode, Guid? OperationKey = null);
public sealed record ConfirmPlanChangePayment([Required, MaxLength(200)] string CardToken,
    [Required, MaxLength(50)] string PaymentMethod);
public sealed record PlanChangeRequestResponse(Guid Id, Guid TargetPlanId, string Reason, string State,
    DateTimeOffset? EffectiveAt, decimal? SourcePrice = null, decimal? TargetPrice = null,
    decimal? AdditionalAmount = null, string? Currency = null, DateTimeOffset? CycleEnd = null,
    string? Issue = null, bool PaymentApproved = false, bool RecurrenceConfirmed = false, DateTimeOffset? QuoteExpiresAt = null,
    string? TargetPlanCode = null)
{
    public static PlanChangeRequestResponse From(PlanChange change) => new(change.Id, change.ToPlanId, change.Reason.ToString(),
        change.Status switch
        {
            PlanChangeStatus.Effective => "Effective",
            PlanChangeStatus.Failed => "Failed",
            _ when change.Issue is not null && change.HasExternalEffects => "RequiresReconciliation",
            _ when change.Reason == PlanChangeReason.Downgrade => "AwaitingCycleConfirmation",
            _ when change.AdditionalAmount is > 0 && !change.PaymentDispatchedAt.HasValue => "AwaitingPaymentConsent",
            _ when change.AdditionalAmount is > 0 && !change.PaymentApprovedAt.HasValue => "AwaitingPaymentConfirmation",
            _ => "AwaitingRecurringConfirmation"
        }, change.EffectiveAtUtc, change.SourcePrice, change.RequestedPrice, change.AdditionalAmount,
        change.RequestedCurrency, change.CycleEnd, change.Issue, change.PaymentApprovedAt.HasValue,
        change.RecurrenceConfirmedAt.HasValue, change.AdditionalAmount > 0 ? change.ChangedAt.AddMinutes(15) : null);
}

/// <summary>Representação de mudança de plano.</summary>
public sealed record PlanChangeResponse(
    Guid Id, Guid? FromPlanId, Guid ToPlanId, string Reason, DateTimeOffset ChangedAt);

/// <summary>Sugestão de upgrade por excedente de profissionais.</summary>
public sealed record UpgradeSuggestionResponse(
    string CurrentPlanCode, string RequiredPlanCode, int ProfessionalCount);

/// <summary>Representação de assinatura.</summary>
public sealed record SubscriptionResponse(
    Guid Id, string PlanCode, string PlanName, string Status,
    DateOnly PeriodStart, DateOnly PeriodEnd, DateOnly? TrialEndsAt,
    DateTimeOffset? TrialStartedAtUtc = null, DateTimeOffset? TrialEndsAtUtc = null,
    DateTimeOffset? NextChargeAtUtc = null, DateTimeOffset? CancellationEffectiveAtUtc = null,
    DateTimeOffset? PastDueSince = null, bool CancellationRequested = false, string? BillingAccess = null,
    string? FirstPaymentStatus = null);

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
