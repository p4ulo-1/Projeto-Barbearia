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

        if (professionalId.HasValue)
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
