using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Servia.Api.Authorization;
using Servia.Api.Contracts;
using Servia.Application.Appointments;
using Servia.Domain.Appointments;
using Servia.Domain.Common;
using Servia.Domain.Identity;
using Servia.Domain.Team;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Identity;
using Servia.Infrastructure.Persistence;

namespace Servia.Api.Controllers;

/// <summary>
/// Endpoints de agenda: disponibilidade, criação, remarcação e cancelamento (F13.4).
///
/// A disponibilidade é calculada a partir da jornada do profissional,
/// descontando bloqueios e agendamentos ativos. O cliente não vê horário
/// que não pode ser reservado.
/// </summary>
[ApiController]
[Route("api/v1/agenda")]
[Authorize]
public sealed class AgendaController(
    AppDbContext context,
    AppointmentService appointmentService,
    AvailabilityService availabilityService,
    ITenantContext tenantContext,
    TimeProvider timeProvider) : ControllerBase
{
    /// <summary>
    /// Garante que o usuário autenticado pode administrar o compromisso do
    /// profissional informado (seção 3 do pedido de ajustes).
    ///
    /// Profissional comum: só a própria agenda. Recepcionista e
    /// administrador: qualquer profissional do estabelecimento.
    /// </summary>
    private async Task EnsureCanManageAsync(Guid professionalId, CancellationToken cancellationToken)
    {
        var role = ResolveRole();
        var userProfessionalId = await ResolveUserProfessionalIdAsync(cancellationToken);

        AppointmentAccessPolicy.EnsureCanManage(role, userProfessionalId, professionalId);
    }

    /// <summary>Papel do usuário autenticado, a partir da claim <c>role</c>.</summary>
    private Role ResolveRole()
    {
        var claim = User.FindFirst(JwtTokenIssuer.RoleClaim)?.Value;

        return Enum.TryParse<Role>(claim, out var role)
            ? role
            : throw new DomainRuleException("Usuário não identificado.");
    }

    /// <summary>
    /// Profissional vinculado ao usuário autenticado, quando houver.
    /// Um administrador ou recepcionista que também atende tem vínculo.
    /// </summary>
    private async Task<Guid?> ResolveUserProfessionalIdAsync(CancellationToken cancellationToken)
    {
        var userId = ResolveUserId();

        return await context.Professionals
            .Where(professional => professional.UserId == userId)
            .Select(professional => (Guid?)professional.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private Guid ResolveUserId()
    {
        var claim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(claim, out var userId)
            ? userId
            : throw new DomainRuleException("Usuário não identificado.");
    }

    /// <summary>
    /// Consulta os horários disponíveis de um profissional em uma data.
    /// </summary>
    [HttpGet("availability")]
    [RequirePermission(Permission.ViewTeamAgenda)]
    [ProducesResponseType<AvailabilityResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAvailabilityAsync(
        [FromQuery] Guid professionalId,
        [FromQuery] DateOnly date,
        [FromQuery, Range(1, 1440)] int durationMinutes,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId
            ?? throw new DomainRuleException("Estabelecimento não identificado.");

        // C9: disponibilidade centralizada. O controller nao recalcula regras.
        var slots = await availabilityService.GetAvailableSlotsAsync(
            tenantId, professionalId, date, durationMinutes, cancellationToken);

        return Ok(new AvailabilityResponse(professionalId, date, slots));
    }

    /// <summary>Cria um agendamento.</summary>
    [HttpPost("appointments")]
    [RequirePermission(Permission.ManageAppointments)]
    [ProducesResponseType<AppointmentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tenantId = tenantContext.TenantId
            ?? throw new DomainRuleException("Estabelecimento não identificado.");

        // Seção 3: profissional comum só cria na própria agenda.
        await EnsureCanManageAsync(request.ProfessionalId, cancellationToken);

        var result = await appointmentService.CreateAsync(
            new CreateAppointmentCommand(
                tenantId,
                request.CustomerId,
                request.ServiceId,
                request.ProfessionalId,
                request.Date,
                request.Time,
                request.DurationMinutes),
            cancellationToken);

        if (result.IsSuccess)
        {
            var body = AppointmentResponse.From(result.Appointment!);
            return Created($"/api/v1/agenda/appointments/{body.Id}", body);
        }

        var failure = result.Failure!;

        return failure.Kind == AppointmentFailureKind.Conflict
            ? Conflict(new ErrorResponse(failure.Message))
            : BadRequest(new ErrorResponse(failure.Message));
    }

    /// <summary>Lista os agendamentos de uma data.</summary>
    [HttpGet("appointments")]
    [RequirePermission(Permission.ViewTeamAgenda)]
    [ProducesResponseType<IReadOnlyList<AppointmentResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync(
        [FromQuery] DateOnly date,
        [FromQuery] Guid? professionalId,
        CancellationToken cancellationToken)
    {
        var query = context.Appointments.Where(appointment => appointment.Date == date);

        // Seção 3: o profissional comum só enxerga a própria agenda. Os
        // demais papéis podem filtrar por qualquer profissional.
        var role = ResolveRole();

        if (AppointmentAccessPolicy.CanManageOnlyOwnAppointments(role))
        {
            var ownProfessionalId = await ResolveUserProfessionalIdAsync(cancellationToken)
                ?? throw new DomainRuleException(
                    "Seu usuário não está vinculado a um profissional.");

            query = query.Where(appointment => appointment.ProfessionalId == ownProfessionalId);
        }
        else if (professionalId.HasValue)
        {
            query = query.Where(appointment => appointment.ProfessionalId == professionalId.Value);
        }

        var items = await query
            .OrderBy(appointment => appointment.Time)
            .Select(appointment => AppointmentResponse.From(appointment))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    /// <summary>Remarca um agendamento.</summary>
    [HttpPatch("appointments/{id:guid}/reschedule")]
    [RequirePermission(Permission.ManageAppointments)]
    [ProducesResponseType<AppointmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RescheduleAsync(
        Guid id,
        [FromBody] RescheduleRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var appointment = await context.Appointments
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (appointment is null)
        {
            throw NotFoundException.For("Agendamento", id);
        }

        // Seção 3: profissional comum só remarca a própria agenda.
        await EnsureCanManageAsync(appointment.ProfessionalId, cancellationToken);

        appointment.Reschedule(request.Date, request.Time);
        await context.SaveChangesAsync(cancellationToken);

        return Ok(AppointmentResponse.From(appointment));
    }

    /// <summary>Cancela um agendamento.</summary>
    [HttpPost("appointments/{id:guid}/cancel")]
    [RequirePermission(Permission.CancelAnyAppointment)]
    [ProducesResponseType<AppointmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelAsync(
        Guid id,
        [FromBody] CancelRequest? request,
        CancellationToken cancellationToken)
    {
        var appointment = await context.Appointments
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (appointment is null)
        {
            throw NotFoundException.For("Agendamento", id);
        }

        // Seção 3: profissional comum só cancela a própria agenda.
        await EnsureCanManageAsync(appointment.ProfessionalId, cancellationToken);

        appointment.Cancel(request?.Reason);
        await context.SaveChangesAsync(cancellationToken);

        return Ok(AppointmentResponse.From(appointment));
    }

    /// <summary>Conclui um agendamento, registrando o atendimento realizado.</summary>
    [HttpPost("appointments/{id:guid}/complete")]
    [RequirePermission(Permission.ManageAppointments)]
    [ProducesResponseType<AppointmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CompleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var appointment = await context.Appointments
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (appointment is null)
        {
            throw NotFoundException.For("Agendamento", id);
        }

        // Seção 3: profissional comum só conclui a própria agenda.
        await EnsureCanManageAsync(appointment.ProfessionalId, cancellationToken);

        appointment.Complete(timeProvider.GetUtcNow());
        await context.SaveChangesAsync(cancellationToken);

        return Ok(AppointmentResponse.From(appointment));
    }
}

/// <summary>Resposta de disponibilidade.</summary>
public sealed record AvailabilityResponse(
    Guid ProfessionalId,
    DateOnly Date,
    IReadOnlyList<TimeOnly> AvailableSlots);

/// <summary>Corpo de remarcação.</summary>
public sealed record RescheduleRequest(DateOnly Date, TimeOnly Time);

/// <summary>Corpo de cancelamento.</summary>
public sealed record CancelRequest([MaxLength(500)] string? Reason = null);
