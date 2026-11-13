using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Servia.Api.Authorization;
using Servia.Api.Contracts;
using Servia.Domain.Appointments;
using Servia.Domain.Common;
using Servia.Domain.Identity;
using Servia.Domain.Team;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;

namespace Servia.Api.Controllers;

/// <summary>
/// Endpoints de equipe: profissionais, vínculos e bloqueios (F13.2).
/// </summary>
[ApiController]
[Route("api/v1/team")]
[Authorize]
public sealed class TeamController(
    AppDbContext context,
    ITenantContext tenantContext,
    IProfessionalCountReader professionalCounts) : ControllerBase
{
    /// <summary>
    /// Garante que o estabelecimento comporta mais um prestador de serviços,
    /// segundo o plano contratado. A validação é feita no backend: o cliente
    /// não decide o limite.
    /// </summary>
    private async Task EnsurePlanAllowsAnotherProviderAsync(CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();

        var subscription = await context.Subscriptions
            .Where(item => item.TenantId == tenantId)
            .OrderByDescending(item => item.CurrentPeriodStart)
            .FirstOrDefaultAsync(cancellationToken);

        if (subscription is null)
        {
            // Sem assinatura, não há limite de plano a aplicar.
            return;
        }

        var plan = await context.Plans
            .FirstOrDefaultAsync(item => item.Id == subscription.PlanId, cancellationToken);

        if (plan is null || plan.HasUnlimitedProfessionals)
        {
            return;
        }

        var current = await professionalCounts.CountActiveAsync(tenantId, cancellationToken);

        if (current + 1 > plan.IncludedProfessionals)
        {
            throw new DomainRuleException(
                $"O plano {plan.Name} comporta até {plan.IncludedProfessionals} profissionais "
                + $"e o estabelecimento já tem {current}. "
                + "Faça upgrade do plano para adicionar mais prestadores de serviço.");
        }
    }

    /// <summary>Lista os profissionais, com paginação.</summary>
    [HttpGet("professionals")]
    [RequirePermission(Permission.ViewTeamAgenda)]
    [ProducesResponseType<PagedResponse<ProfessionalResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListProfessionalsAsync(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        var request = PageRequest.Normalize(page, pageSize);
        var query = context.Professionals.Where(professional => professional.IsActive);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(professional => professional.Name)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(professional => new ProfessionalResponse(
                professional.Id,
                professional.Name,
                professional.Title,
                professional.IsActive,
                professional.IsServiceProvider))
            .ToListAsync(cancellationToken);

        return Ok(PagedResponse<ProfessionalResponse>.Create(items, request, total));
    }

    /// <summary>Cria um profissional com jornada de trabalho.</summary>
    [HttpPost("professionals")]
    [RequirePermission(Permission.ManageTeam)]
    [ProducesResponseType<ProfessionalResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateProfessionalAsync(
        [FromBody] CreateProfessionalRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tenantId = RequireTenant();

        // Seção 2: só prestador de serviços ocupa vaga no plano. A validação
        // do limite é feita no backend antes de gravar.
        if (request.IsServiceProvider)
        {
            await EnsurePlanAllowsAnotherProviderAsync(cancellationToken);
        }

        var schedule = WorkSchedule.Create(
            request.WorkDays,
            request.StartTime,
            request.EndTime,
            request.SlotIntervalMinutes);

        var professional = Professional.Create(
            tenantId, request.Name, schedule, request.Title, request.Bio,
            isServiceProvider: request.IsServiceProvider);

        context.Professionals.Add(professional);
        await context.SaveChangesAsync(cancellationToken);

        return Created(
            $"/api/v1/team/professionals/{professional.Id}",
            new ProfessionalResponse(
                professional.Id, professional.Name, professional.Title,
                professional.IsActive, professional.IsServiceProvider));
    }

    /// <summary>
    /// Define se o profissional presta serviços (seção 2).
    ///
    /// Ao passar a prestar, ocupa vaga no plano — o limite é validado no
    /// backend. Ao deixar de prestar, a vaga é liberada. Nenhum usuário ou
    /// compromisso é excluído.
    /// </summary>
    [HttpPatch("professionals/{id:guid}/service-provider")]
    [RequirePermission(Permission.ManageTeam)]
    [ProducesResponseType<ProfessionalResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetServiceProviderAsync(
        Guid id,
        [FromBody] SetServiceProviderRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var professional = await context.Professionals
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw NotFoundException.For("Profissional", id);

        var startsProviding = request.IsServiceProvider && !professional.IsServiceProvider;

        if (startsProviding)
        {
            await EnsurePlanAllowsAnotherProviderAsync(cancellationToken);
        }

        professional.SetServiceProvider(request.IsServiceProvider);
        await context.SaveChangesAsync(cancellationToken);

        return Ok(new ProfessionalResponse(
            professional.Id, professional.Name, professional.Title,
            professional.IsActive, professional.IsServiceProvider));
    }

    /// <summary>Vincula um profissional a um serviço.</summary>
    [HttpPost("professionals/{id:guid}/services/{serviceId:guid}")]
    [RequirePermission(Permission.ManageTeam)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LinkServiceAsync(
        Guid id,
        Guid serviceId,
        CancellationToken cancellationToken)
    {
        var link = ProfessionalService.Create(RequireTenant(), id, serviceId);

        context.ProfessionalServices.Add(link);
        await context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>Lista os serviços que um profissional executa.</summary>
    [HttpGet("professionals/{id:guid}/services")]
    [RequirePermission(Permission.ViewTeamAgenda)]
    [ProducesResponseType<IReadOnlyList<Guid>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListProfessionalServicesAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var serviceIds = await context.ProfessionalServices
            .Where(link => link.ProfessionalId == id)
            .Select(link => link.ServiceId)
            .ToListAsync(cancellationToken);

        return Ok(serviceIds);
    }

    /// <summary>Bloqueia um período na agenda do profissional.</summary>
    [HttpPost("professionals/{id:guid}/blocks")]
    [RequirePermission(Permission.ManageTeam)]
    [ProducesResponseType<BlockResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateBlockAsync(
        Guid id,
        [FromBody] CreateBlockRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var block = ScheduleBlock.Create(
            RequireTenant(), id, request.Start, request.End, request.Reason);

        context.ScheduleBlocks.Add(block);
        await context.SaveChangesAsync(cancellationToken);

        return Created(
            $"/api/v1/team/professionals/{id}/blocks/{block.Id}",
            new BlockResponse(block.Id, block.Start, block.End, block.Reason));
    }

    /// <summary>Lista os bloqueios de um profissional.</summary>
    [HttpGet("professionals/{id:guid}/blocks")]
    [RequirePermission(Permission.ViewTeamAgenda)]
    [ProducesResponseType<IReadOnlyList<BlockResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListBlocksAsync(Guid id, CancellationToken cancellationToken)
    {
        var blocks = await context.ScheduleBlocks
            .Where(block => block.ProfessionalId == id)
            .OrderBy(block => block.Start)
            .Select(block => new BlockResponse(block.Id, block.Start, block.End, block.Reason))
            .ToListAsync(cancellationToken);

        return Ok(blocks);
    }

    private Guid RequireTenant() =>
        tenantContext.TenantId ?? throw new DomainRuleException("Estabelecimento não identificado.");
}

/// <summary>Corpo de criação de profissional.</summary>
/// <param name="IsServiceProvider">
/// Indica se a pessoa presta serviços e ocupa vaga no plano. Padrão: sim.
/// </param>
public sealed record CreateProfessionalRequest(
    [Required, MaxLength(150)] string Name,
    [Required, MinLength(1)] IReadOnlyList<DayOfWeek> WorkDays,
    TimeOnly StartTime,
    TimeOnly EndTime,
    [Range(5, 120)] int SlotIntervalMinutes = 30,
    [MaxLength(100)] string? Title = null,
    [MaxLength(1000)] string? Bio = null,
    bool IsServiceProvider = true);

/// <summary>Corpo de definição da atuação profissional.</summary>
public sealed record SetServiceProviderRequest(bool IsServiceProvider);

/// <summary>Corpo de criação de bloqueio.</summary>
public sealed record CreateBlockRequest(
    DateTimeOffset Start,
    DateTimeOffset End,
    [MaxLength(300)] string? Reason = null);

/// <summary>Representação de profissional.</summary>
public sealed record ProfessionalResponse(
    Guid Id, string Name, string? Title, bool IsActive, bool IsServiceProvider);

/// <summary>Representação de bloqueio.</summary>
public sealed record BlockResponse(Guid Id, DateTimeOffset Start, DateTimeOffset End, string? Reason);
