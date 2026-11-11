using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Servia.Api.Authorization;
using Servia.Api.Contracts;
using Servia.Domain.Common;
using Servia.Domain.Identity;
using Servia.Domain.Loyalty;
using Servia.Domain.Memberships;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;

namespace Servia.Api.Controllers;

/// <summary>
/// Endpoints dos módulos opcionais (F13.9): clubes, pacotes, fidelidade e NPS.
///
/// Todos os planos liberam todos os módulos (F4.9): o que varia é a franquia
/// de consumo, não o acesso.
/// </summary>
[ApiController]
[Route("api/v1/modules")]
[Authorize]
public sealed class ModulesController(
    AppDbContext context,
    ITenantContext tenantContext,
    TimeProvider timeProvider) : ControllerBase
{
    // ---------- Clubes ----------

    /// <summary>Lista os clubes de assinatura.</summary>
    [HttpGet("membership-plans")]
    [RequirePermission(Permission.ViewServices)]
    [ProducesResponseType<IReadOnlyList<MembershipPlanResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListMembershipPlansAsync(CancellationToken cancellationToken)
    {
        var plans = await context.MembershipPlans
            .Where(plan => plan.IsActive)
            .Select(plan => new MembershipPlanResponse(
                plan.Id, plan.Name, plan.MonthlyPrice.Amount, plan.Benefits.Count))
            .ToListAsync(cancellationToken);

        return Ok(plans);
    }

    /// <summary>Cria um clube de assinatura.</summary>
    [HttpPost("membership-plans")]
    [RequirePermission(Permission.ManageServices)]
    [ProducesResponseType<MembershipPlanResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateMembershipPlanAsync(
        [FromBody] CreateMembershipPlanRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var plan = MembershipPlan.Create(
            RequireTenant(), request.Name, Money.From(request.MonthlyPrice));

        foreach (var benefit in request.Benefits)
        {
            if (benefit.DiscountPercentage.HasValue)
            {
                plan.AddDiscountBenefit(benefit.DiscountPercentage.Value);
            }
            else if (benefit.ServiceId.HasValue && benefit.QuantityPerPeriod.HasValue)
            {
                plan.AddServiceBenefit(benefit.ServiceId.Value, benefit.QuantityPerPeriod.Value);
            }
        }

        context.MembershipPlans.Add(plan);
        await context.SaveChangesAsync(cancellationToken);

        return Created(
            $"/api/v1/modules/membership-plans/{plan.Id}",
            new MembershipPlanResponse(plan.Id, plan.Name, plan.MonthlyPrice.Amount, plan.Benefits.Count));
    }

    /// <summary>Assina um cliente a um clube.</summary>
    [HttpPost("memberships")]
    [RequirePermission(Permission.ManageCustomers)]
    [ProducesResponseType<MembershipResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> SubscribeAsync(
        [FromBody] SubscribeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var membership = Membership.Subscribe(
            RequireTenant(),
            request.CustomerId,
            request.MembershipPlanId,
            request.PeriodStart,
            request.PeriodEnd);

        context.Memberships.Add(membership);
        await context.SaveChangesAsync(cancellationToken);

        return Created(
            $"/api/v1/modules/memberships/{membership.Id}",
            new MembershipResponse(membership.Id, membership.CustomerId,
                membership.Status.ToString(), membership.CurrentPeriodEnd));
    }

    // ---------- Pacotes ----------

    /// <summary>Lista os pacotes de serviço.</summary>
    [HttpGet("packages")]
    [RequirePermission(Permission.ViewServices)]
    [ProducesResponseType<IReadOnlyList<PackageResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPackagesAsync(CancellationToken cancellationToken)
    {
        var packages = await context.ServicePackages
            .Where(package => package.IsActive)
            .Select(package => new PackageResponse(
                package.Id, package.Name, package.Price.Amount,
                package.SessionsIncluded, package.ValidityDays))
            .ToListAsync(cancellationToken);

        return Ok(packages);
    }

    /// <summary>Cria um pacote de serviço.</summary>
    [HttpPost("packages")]
    [RequirePermission(Permission.ManageServices)]
    [ProducesResponseType<PackageResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreatePackageAsync(
        [FromBody] CreatePackageRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var package = ServicePackage.Create(
            RequireTenant(), request.Name, Money.From(request.Price),
            request.SessionsIncluded, request.ValidityDays);

        context.ServicePackages.Add(package);
        await context.SaveChangesAsync(cancellationToken);

        return Created(
            $"/api/v1/modules/packages/{package.Id}",
            new PackageResponse(package.Id, package.Name, package.Price.Amount,
                package.SessionsIncluded, package.ValidityDays));
    }

    // ---------- Fidelidade ----------

    /// <summary>Consulta o saldo de pontos de um cliente.</summary>
    [HttpGet("loyalty/{customerId:guid}")]
    [RequirePermission(Permission.ViewCustomers)]
    [ProducesResponseType<LoyaltyResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLoyaltyAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var account = await context.LoyaltyAccounts
            .FirstOrDefaultAsync(item => item.CustomerId == customerId, cancellationToken);

        return Ok(account is null
            ? new LoyaltyResponse(customerId, 0, 0, 0)
            : new LoyaltyResponse(customerId, account.Points, account.TotalEarned, account.TotalRedeemed));
    }

    /// <summary>Credita pontos por atendimento concluído.</summary>
    [HttpPost("loyalty/{customerId:guid}/earn")]
    [RequirePermission(Permission.ManageCustomers)]
    [ProducesResponseType<LoyaltyResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> EarnPointsAsync(
        Guid customerId,
        [FromBody] EarnPointsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var account = await context.LoyaltyAccounts
            .FirstOrDefaultAsync(item => item.CustomerId == customerId, cancellationToken);

        if (account is null)
        {
            account = LoyaltyAccount.Open(RequireTenant(), customerId);
            context.LoyaltyAccounts.Add(account);
        }

        account.Earn(request.Points);
        await context.SaveChangesAsync(cancellationToken);

        return Ok(new LoyaltyResponse(customerId, account.Points,
            account.TotalEarned, account.TotalRedeemed));
    }

    // ---------- NPS ----------

    /// <summary>Registra a avaliação de um atendimento.</summary>
    [HttpPost("reviews")]
    [RequirePermission(Permission.ManageCustomers)]
    [ProducesResponseType<ReviewResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateReviewAsync(
        [FromBody] CreateReviewRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var review = ServiceReview.Create(
            RequireTenant(),
            request.AppointmentId,
            request.CustomerId,
            request.Score,
            timeProvider.GetUtcNow(),
            request.Comment);

        context.ServiceReviews.Add(review);
        await context.SaveChangesAsync(cancellationToken);

        return Created(
            $"/api/v1/modules/reviews/{review.Id}",
            new ReviewResponse(review.Id, review.Score, review.Category.ToString()));
    }

    /// <summary>Calcula o NPS do período.</summary>
    [HttpGet("nps")]
    [RequirePermission(Permission.ViewFinancialReports)]
    [ProducesResponseType<NpsResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNpsAsync(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken)
    {
        var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var end = new DateTimeOffset(to.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

        // O SQLite nao traduz comparacao de DateTimeOffset com offset zero.
        var allReviews = await context.ServiceReviews.ToListAsync(cancellationToken);

        var reviews = allReviews
            .Where(review => review.CreatedAt >= start && review.CreatedAt <= end)
            .ToList();

        var report = NpsReport.Build(reviews);

        return Ok(new NpsResponse(
            report.TotalResponses, report.Promoters, report.Passives,
            report.Detractors, report.Score));
    }

    private Guid RequireTenant() =>
        tenantContext.TenantId ?? throw new DomainRuleException("Estabelecimento não identificado.");
}

/// <summary>Corpo de criação de clube.</summary>
public sealed record CreateMembershipPlanRequest(
    [Required, MaxLength(150)] string Name,
    [Range(0.01, 100_000)] decimal MonthlyPrice,
    IReadOnlyList<BenefitRequest> Benefits);

/// <summary>Benefício de um clube.</summary>
public sealed record BenefitRequest(
    Guid? ServiceId = null,
    int? QuantityPerPeriod = null,
    decimal? DiscountPercentage = null);

/// <summary>Corpo de assinatura de clube.</summary>
public sealed record SubscribeRequest(
    Guid CustomerId, Guid MembershipPlanId, DateOnly PeriodStart, DateOnly PeriodEnd);

/// <summary>Corpo de criação de pacote.</summary>
public sealed record CreatePackageRequest(
    [Required, MaxLength(150)] string Name,
    [Range(0.01, 100_000)] decimal Price,
    [Range(1, 1000)] int SessionsIncluded,
    [Range(1, 3650)] int ValidityDays);

/// <summary>Corpo de crédito de pontos.</summary>
public sealed record EarnPointsRequest([Range(1, 100_000)] int Points);

/// <summary>Corpo de avaliação.</summary>
public sealed record CreateReviewRequest(
    Guid AppointmentId,
    Guid CustomerId,
    [Range(0, 10)] int Score,
    [MaxLength(1000)] string? Comment = null);

/// <summary>Representação de clube.</summary>
public sealed record MembershipPlanResponse(
    Guid Id, string Name, decimal MonthlyPrice, int BenefitCount);

/// <summary>Representação de assinatura de clube.</summary>
public sealed record MembershipResponse(
    Guid Id, Guid CustomerId, string Status, DateOnly PeriodEnd);

/// <summary>Representação de pacote.</summary>
public sealed record PackageResponse(
    Guid Id, string Name, decimal Price, int SessionsIncluded, int ValidityDays);

/// <summary>Representação de fidelidade.</summary>
public sealed record LoyaltyResponse(
    Guid CustomerId, int Points, int TotalEarned, int TotalRedeemed);

/// <summary>Representação de avaliação.</summary>
public sealed record ReviewResponse(Guid Id, int Score, string Category);

/// <summary>Representação de NPS.</summary>
public sealed record NpsResponse(
    int TotalResponses, int Promoters, int Passives, int Detractors, decimal Score);
