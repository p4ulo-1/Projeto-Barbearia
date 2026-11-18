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
    /// Consulta os horários disponíveis de um profissional para um serviço.
    ///
    /// O serviço é obrigatório: sem ele não há duração nem vínculo a
    /// verificar, e a disponibilidade seria calculada sobre um intervalo
    /// arbitrário.
    ///
    /// Requisição sem serviço é <b>400</b>, não 200 com lista vazia: uma lista
    /// vazia significa "não há horário livre", que é uma resposta diferente de
    /// "o pedido está incompleto".
    /// </summary>
    [HttpGet("availability")]
    [RequirePermission(Permission.ViewTeamAgenda)]
    [ProducesResponseType<AvailabilityResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAvailabilityAsync(
        [FromQuery] Guid professionalId,
        [FromQuery] Guid serviceId,
        [FromQuery] DateOnly date,
        CancellationToken cancellationToken)
    {
        if (professionalId == Guid.Empty)
        {
            return BadRequest(new ErrorResponse("O profissional é obrigatório."));
        }

        if (serviceId == Guid.Empty)
        {
            return BadRequest(new ErrorResponse("O serviço é obrigatório."));
        }

        if (date == default)
        {
            return BadRequest(new ErrorResponse("A data é obrigatória."));
        }

        var tenantId = tenantContext.TenantId
            ?? throw new DomainRuleException("Estabelecimento não identificado.");

        // C9: disponibilidade centralizada. O controller nao recalcula regras.
        var slots = await availabilityService.GetAvailableSlotsAsync(
            tenantId, professionalId, serviceId, date, cancellationToken);

        return Ok(new AvailabilityResponse(professionalId, serviceId, date, slots));
    }

    /// <summary>Identifica o profissional vinculado ao usuário, sem aceitar um ID do cliente.</summary>
    [HttpGet("me")]
    [RequirePermission(Permission.ViewOwnAgenda)]
    public async Task<IActionResult> MeAsync(CancellationToken cancellationToken)
    {
        var id = await ResolveUserProfessionalIdAsync(cancellationToken);
        return id.HasValue ? Ok(new OwnAgendaResponse(id.Value)) : NotFound(new ErrorResponse("Usuário sem profissional vinculado."));
    }

    /// <summary>Disponibilidade apenas do profissional vinculado ao usuário.</summary>
    [HttpGet("mine/availability")]
    [RequirePermission(Permission.ViewOwnAgenda)]
    public async Task<IActionResult> OwnAvailabilityAsync(
        [FromQuery] Guid serviceId, [FromQuery] DateOnly date, CancellationToken cancellationToken)
    {
        if (serviceId == Guid.Empty || date == default)
            return BadRequest(new ErrorResponse("Serviço e data são obrigatórios."));
        var id = await ResolveUserProfessionalIdAsync(cancellationToken);
        if (!id.HasValue) return NotFound(new ErrorResponse("Usuário sem profissional vinculado."));
        var tenantId = tenantContext.TenantId ?? throw new DomainRuleException("Estabelecimento não identificado.");
        var slots = await availabilityService.GetAvailableSlotsAsync(tenantId, id.Value, serviceId, date, cancellationToken);
        return Ok(new AvailabilityResponse(id.Value, serviceId, date, slots));
    }

    /// <summary>Consulta de remarcação com o compromisso e sua duração persistidos.</summary>
    [HttpGet("appointments/{id:guid}/reschedule-availability")]
    [RequirePermission(Permission.ManageAppointments)]
    public async Task<IActionResult> RescheduleAvailabilityAsync(
        Guid id, [FromQuery] DateOnly date, CancellationToken cancellationToken)
    {
        if (date == default) return BadRequest(new ErrorResponse("A data é obrigatória."));
        var appointment = await context.Appointments.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (appointment is null) return NotFound(new ErrorResponse("Agendamento não encontrado."));
        await EnsureCanManageAsync(appointment.ProfessionalId, cancellationToken);
        if (!appointment.IsConfirmed) return BadRequest(new ErrorResponse("Somente agendamentos confirmados podem ser remarcados."));
        var tenantId = tenantContext.TenantId ?? throw new DomainRuleException("Estabelecimento não identificado.");
        var evaluation = await availabilityService.EvaluateAsync(
            tenantId, appointment.ProfessionalId, appointment.ServiceId, date, null, cancellationToken,
            excludeAppointmentId: appointment.Id, durationMinutesOverride: appointment.DurationMinutes);
        return Ok(new AvailabilityResponse(appointment.ProfessionalId, appointment.ServiceId, date, evaluation.Slots));
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
                request.Time),
            cancellationToken);

        if (result.IsSuccess)
        {
            var body = AppointmentResponse.From(result.Appointment!);
            return Created($"/api/v1/agenda/appointments/{body.Id}", body);
        }

        var failure = result.Failure!;

        return failure.Kind switch
        {
            AppointmentFailureKind.Conflict => Conflict(new ErrorResponse(failure.Message)),
            AppointmentFailureKind.NotFound => NotFound(new ErrorResponse(failure.Message)),
            _ => BadRequest(new ErrorResponse(failure.Message))
        };
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

    /// <summary>Lista somente os compromissos do profissional vinculado ao usuário.</summary>
    [HttpGet("mine/appointments")]
    [RequirePermission(Permission.ViewOwnAgenda)]
    public async Task<IActionResult> OwnListAsync([FromQuery] DateOnly date, CancellationToken cancellationToken)
    {
        if (date == default) return BadRequest(new ErrorResponse("A data é obrigatória."));
        var id = await ResolveUserProfessionalIdAsync(cancellationToken);
        if (!id.HasValue) return NotFound(new ErrorResponse("Usuário sem profissional vinculado."));
        var items = await context.Appointments
            .Where(item => item.ProfessionalId == id.Value && item.Date == date)
            .OrderBy(item => item.Time)
            .Select(item => AppointmentResponse.From(item))
            .ToListAsync(cancellationToken);
        return Ok(items);
    }

    /// <summary>
    /// Remarca um agendamento.
    ///
    /// A validação (serviço, vínculo, jornada, bloqueio, conflito) pertence
    /// ao caso de uso, não ao controller. Aqui só se resolve o tenant, se
    /// verifica a autorização e se traduz o resultado em HTTP.
    /// </summary>
    [HttpPatch("appointments/{id:guid}/reschedule")]
    [RequirePermission(Permission.ManageAppointments)]
    [ProducesResponseType<AppointmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RescheduleAsync(
        Guid id,
        [FromBody] RescheduleRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tenantId = tenantContext.TenantId
            ?? throw new DomainRuleException("Estabelecimento não identificado.");

        // Seção 3: profissional comum só remarca a própria agenda.
        // O profissional do agendamento é lido antes da remarcação; o caso de
        // uso revalida a existência dentro da transação.
        var professionalId = await context.Appointments
            .Where(item => item.Id == id)
            .Select(item => (Guid?)item.ProfessionalId)
            .FirstOrDefaultAsync(cancellationToken);

        if (professionalId is null)
        {
            throw NotFoundException.For("Agendamento", id);
        }

        await EnsureCanManageAsync(professionalId.Value, cancellationToken);

        var result = await appointmentService.RescheduleAsync(
            tenantId, id, request.Date, request.Time, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(AppointmentResponse.From(result.Appointment!));
        }

        var failure = result.Failure!;

        return failure.Kind switch
        {
            AppointmentFailureKind.Conflict => Conflict(new ErrorResponse(failure.Message)),
            AppointmentFailureKind.NotFound => NotFound(new ErrorResponse(failure.Message)),
            _ => BadRequest(new ErrorResponse(failure.Message))
        };
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

    /// <summary>Cancela apenas compromisso próprio, com permissão de agenda própria.</summary>
    [HttpPost("mine/appointments/{id:guid}/cancel")]
    [RequirePermission(Permission.ViewOwnAgenda)]
    [RequirePermission(Permission.ManageAppointments)]
    public async Task<IActionResult> CancelOwnAsync(
        Guid id, [FromBody] CancelRequest? request, CancellationToken cancellationToken)
    {
        var appointment = await context.Appointments.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (appointment is null) return NotFound(new ErrorResponse("Agendamento não encontrado."));
        var ownId = await ResolveUserProfessionalIdAsync(cancellationToken);
        if (!ownId.HasValue || appointment.ProfessionalId != ownId.Value)
            return Forbid();
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
    Guid ServiceId,
    DateOnly Date,
    IReadOnlyList<TimeOnly> AvailableSlots);

public sealed record OwnAgendaResponse(Guid ProfessionalId);

/// <summary>Corpo de remarcação.</summary>
public sealed record RescheduleRequest(DateOnly Date, TimeOnly Time);

/// <summary>Corpo de cancelamento.</summary>
public sealed record CancelRequest([MaxLength(500)] string? Reason = null);
