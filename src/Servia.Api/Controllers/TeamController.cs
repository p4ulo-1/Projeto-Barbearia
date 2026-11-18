using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Servia.Api.Authorization;
using Servia.Api.Contracts;
using Servia.Application.Billing;
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
    PlanSlotService planSlots,
    ISubscriptionRepository subscriptions) : ControllerBase
{
    /// <summary>Lista os profissionais, com paginação.</summary>
    [HttpGet("professionals")]
    [RequirePermission(Permission.ViewTeamAgenda)]
    [ProducesResponseType<PagedResponse<ProfessionalResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListProfessionalsAsync(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken,
        [FromQuery] bool includeInactive = false)
    {
        var request = PageRequest.Normalize(page, pageSize);
        var query = context.Professionals.AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(professional => professional.IsActive);
        }

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

    /// <summary>Consulta os dados cadastrais de um profissional.</summary>
    [HttpGet("professionals/{id:guid}")]
    [RequirePermission(Permission.ViewTeamAgenda)]
    [ProducesResponseType<ProfessionalDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfessionalAsync(Guid id, CancellationToken cancellationToken) =>
        Ok(ProfessionalDetailResponse.From(await FindProfessionalAsync(id, cancellationToken)));

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

        // Um profissional recém-criado não tem serviço associado, então não
        // ocupa vaga: o critério aprovado exige ao menos um serviço ativo
        // vinculado. A vaga é consumida quando o primeiro serviço é
        // associado — e é ali que o limite é verificado.
        //
        // Verificar o limite aqui contaria um cadastro que ainda não pode
        // receber agendamento, bloqueando a montagem da equipe.
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

    /// <summary>Atualiza nome, título e biografia do profissional.</summary>
    [HttpPut("professionals/{id:guid}")]
    [RequirePermission(Permission.ManageTeam)]
    [ProducesResponseType<ProfessionalDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProfessionalAsync(
        Guid id,
        [FromBody] UpdateProfessionalRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var professional = await FindProfessionalAsync(id, cancellationToken);
        professional.UpdateProfile(request.Name, request.Title, request.Bio, professional.PhotoUrl);
        await context.SaveChangesAsync(cancellationToken);

        return Ok(ProfessionalDetailResponse.From(professional));
    }

    /// <summary>Reativa um profissional e verifica a capacidade do plano.</summary>
    [HttpPost("professionals/{id:guid}/activate")]
    [RequirePermission(Permission.ManageTeam)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActivateProfessionalAsync(Guid id, CancellationToken cancellationToken)
    {
        await planSlots.ExecuteWithSlotCheckAsync(RequireTenant(), async token =>
        {
            var professional = await FindProfessionalAsync(id, token);
            professional.Activate();
            await context.SaveChangesAsync(token);
            return true;
        }, cancellationToken);

        return NoContent();
    }

    /// <summary>Desativa um profissional e preserva seus vínculos e atendimentos.</summary>
    [HttpPost("professionals/{id:guid}/deactivate")]
    [RequirePermission(Permission.ManageTeam)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeactivateProfessionalAsync(Guid id, CancellationToken cancellationToken)
    {
        await subscriptions.ExecuteSerializableAsync(async token =>
        {
            var professional = await FindProfessionalAsync(id, token);
            professional.Deactivate();
            await context.SaveChangesAsync(token);
            return true;
        }, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Define se o profissional presta serviços.
    ///
    /// <b>Não consome vaga.</b> A ocupação de vaga é determinada pelo
    /// critério aprovado — ativo, com serviço ativo associado e jornada
    /// configurada — e não por este atributo. Alterá-lo não torna ninguém
    /// elegível nem inelegível, então não há limite a verificar aqui.
    ///
    /// A propriedade permanece porque descreve a atuação da pessoa na equipe,
    /// informação usada na apresentação e na gestão.
    /// </summary>
    [HttpPatch("professionals/{id:guid}/service-provider")]
    [RequirePermission(Permission.ManageTeam)]
    [ProducesResponseType<ProfessionalResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetServiceProviderAsync(
        Guid id,
        [FromBody] SetServiceProviderRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var professional = await FindProfessionalAsync(id, cancellationToken);

        professional.SetServiceProvider(request.IsServiceProvider);
        await context.SaveChangesAsync(cancellationToken);

        return Ok(new ProfessionalResponse(
            professional.Id, professional.Name, professional.Title,
            professional.IsActive, professional.IsServiceProvider));
    }

    /// <summary>
    /// Vincula um profissional a um serviço.
    ///
    /// <b>O vínculo pode tornar o profissional elegível para ocupar vaga.</b>
    /// Antes, um profissional ativo sem serviço não consumia vaga; ao receber
    /// o primeiro serviço ativo, passa a consumir. Sem a verificação aqui,
    /// seria possível contornar o plano cadastrando profissionais sem serviço
    /// e associando-os depois.
    ///
    /// <b>A verificação é atômica.</b> Leitura, contagem, validação e
    /// gravação rodam na mesma transação serializável, via
    /// <see cref="PlanSlotService"/>. Verificar fora da transação permitiria
    /// que duas requisições concorrentes contassem a mesma vaga livre e ambas
    /// gravassem.
    ///
    /// A verificação só se aplica quando o vínculo <b>torna</b> a pessoa
    /// elegível: se ela já ocupava vaga, o vínculo não acrescenta consumo.
    /// </summary>
    [HttpPost("professionals/{id:guid}/services/{serviceId:guid}")]
    [RequirePermission(Permission.ManageTeam)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> LinkServiceAsync(
        Guid id,
        Guid serviceId,
        CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();

        await planSlots.ExecuteWithSlotCheckAsync(tenantId, async token =>
        {
            // Todas as leituras e a persistência pertencem à mesma transação.
            await EnsureLinkTargetsExistAsync(id, serviceId, token);

            if (!await context.ProfessionalServices.AnyAsync(
                link => link.ProfessionalId == id && link.ServiceId == serviceId, token))
            {
                context.ProfessionalServices.Add(ProfessionalService.Create(tenantId, id, serviceId));
                await context.SaveChangesAsync(token);
            }
            return true;
        }, cancellationToken);
        return NoContent();
    }

    /// <summary>Remove o vínculo com um serviço, preservando os atendimentos registrados.</summary>
    [HttpDelete("professionals/{id:guid}/services/{serviceId:guid}")]
    [RequirePermission(Permission.ManageTeam)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UnlinkServiceAsync(Guid id, Guid serviceId, CancellationToken cancellationToken)
    {
        await subscriptions.ExecuteSerializableAsync(async token =>
        {
            await EnsureLinkTargetsExistAsync(id, serviceId, token);
            await context.ProfessionalServices
                .Where(link => link.ProfessionalId == id && link.ServiceId == serviceId)
                .ExecuteDeleteAsync(token);
            return true;
        }, cancellationToken);

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

    private async Task<Professional> FindProfessionalAsync(Guid id, CancellationToken cancellationToken) =>
        await context.Professionals.FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
        ?? throw NotFoundException.For("Profissional", id);

    private async Task EnsureLinkTargetsExistAsync(Guid professionalId, Guid serviceId, CancellationToken cancellationToken)
    {
        if (!await context.Professionals.AnyAsync(item => item.Id == professionalId, cancellationToken))
        {
            throw NotFoundException.For("Profissional", professionalId);
        }

        if (!await context.Services.AnyAsync(item => item.Id == serviceId, cancellationToken))
        {
            throw NotFoundException.For("Serviço", serviceId);
        }
    }

    private Guid RequireTenant() =>
        tenantContext.TenantId ?? throw new DomainRuleException("Estabelecimento não identificado.");
}

/// <summary>Corpo de criação de profissional.</summary>
/// <param name="IsServiceProvider">
/// Descreve a atuação da pessoa; não controla vagas nem habilita reservas. Padrão: sim.
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

/// <summary>Dados cadastrais editáveis do profissional.</summary>
public sealed record UpdateProfessionalRequest(
    [Required, MaxLength(150)] string Name,
    [MaxLength(100)] string? Title = null,
    [MaxLength(1000)] string? Bio = null);

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

/// <summary>Detalhe cadastral do profissional.</summary>
public sealed record ProfessionalDetailResponse(
    Guid Id, string Name, string? Title, string? Bio, bool IsActive, bool IsServiceProvider)
{
    public static ProfessionalDetailResponse From(Professional professional) =>
        new(professional.Id, professional.Name, professional.Title, professional.Bio,
            professional.IsActive, professional.IsServiceProvider);
}

/// <summary>Representação de bloqueio.</summary>
public sealed record BlockResponse(Guid Id, DateTimeOffset Start, DateTimeOffset End, string? Reason);
