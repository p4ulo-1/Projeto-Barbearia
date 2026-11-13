using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Servia.Api.Authorization;
using Servia.Api.Contracts;
using Servia.Domain.Common;
using Servia.Domain.Identity;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;

namespace Servia.Api.Controllers;

/// <summary>
/// Configurações do estabelecimento — expediente semanal (C9).
///
/// <b>Independe dos profissionais:</b> este expediente informa quando o
/// estabelecimento atende ao público; não restringe agendamentos, que
/// dependem só da disponibilidade de cada profissional.
///
/// <b>Segurança:</b> o tenant vem da claim do token. Nenhum identificador é
/// aceito do corpo.
/// </summary>
[ApiController]
[Route("api/v1/tenant/settings")]
[Authorize]
public sealed class TenantSettingsController(
    AppDbContext context,
    ITenantContext tenantContext) : ControllerBase
{
    /// <summary>Consulta o expediente semanal do estabelecimento.</summary>
    [HttpGet("schedule")]
    [RequirePermission(Permission.ViewTenantSettings)]
    [ProducesResponseType<ScheduleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetScheduleAsync(CancellationToken cancellationToken)
    {
        var tenant = await LoadTenantAsync(cancellationToken);

        return Ok(ScheduleResponse.From(tenant.Schedule));
    }

    /// <summary>
    /// Atualiza o expediente semanal. Permite horários diferentes por dia e
    /// dias fechados. Não afeta os agendamentos.
    /// </summary>
    [HttpPut("schedule")]
    [RequirePermission(Permission.ManageTenantSettings)]
    [ProducesResponseType<ScheduleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateScheduleAsync(
        [FromBody] UpdateScheduleRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tenant = await LoadTenantAsync(cancellationToken);

        EstablishmentSchedule schedule;

        try
        {
            schedule = EstablishmentSchedule.Create(
                request.Days.Select(day => day.IsOpen
                    ? EstablishmentDaySchedule.Open(day.DayOfWeek, day.OpenTime!.Value, day.CloseTime!.Value)
                    : EstablishmentDaySchedule.Closed(day.DayOfWeek)));
        }
        catch (DomainRuleException exception)
        {
            return BadRequest(new ErrorResponse(exception.Message));
        }

        tenant.UpdateSchedule(schedule);
        await context.SaveChangesAsync(cancellationToken);

        return Ok(ScheduleResponse.From(tenant.Schedule));
    }

    private async Task<Tenant> LoadTenantAsync(CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId
            ?? throw new DomainRuleException("Estabelecimento não identificado.");

        return await context.Tenants
            .FirstOrDefaultAsync(tenant => tenant.Id == tenantId, cancellationToken)
            ?? throw NotFoundException.For("Estabelecimento", tenantId);
    }
}

/// <summary>Corpo de atualização do expediente.</summary>
public sealed record UpdateScheduleRequest(IReadOnlyList<ScheduleDayRequest> Days);

/// <summary>Expediente de um dia no corpo da requisição.</summary>
public sealed record ScheduleDayRequest(
    DayOfWeek DayOfWeek,
    bool IsOpen,
    TimeOnly? OpenTime,
    TimeOnly? CloseTime);

/// <summary>Representação do expediente.</summary>
public sealed record ScheduleResponse(IReadOnlyList<ScheduleDayResponse> Days)
{
    public static ScheduleResponse From(EstablishmentSchedule schedule) =>
        new(schedule.Days
            .OrderBy(day => (int)day.DayOfWeek)
            .Select(day => new ScheduleDayResponse(
                day.DayOfWeek, day.IsOpen, day.OpenTime, day.CloseTime))
            .ToList());
}

/// <summary>Representação de um dia do expediente.</summary>
public sealed record ScheduleDayResponse(
    DayOfWeek DayOfWeek,
    bool IsOpen,
    TimeOnly? OpenTime,
    TimeOnly? CloseTime);
