using Microsoft.AspNetCore.Mvc;
using Servia.Api.Contracts;
using Servia.Application.Appointments;
using Servia.Domain.Tenancy;

namespace Servia.Api.Controllers;

/// <summary>
/// Endpoints de agendamento.
///
/// O controller é fino de propósito: valida a forma do pedido, delega o caso
/// de uso e traduz o resultado em resposta HTTP. Não contém regra de negócio
/// nem try/catch — o tratamento de exceções é central, no middleware.
/// </summary>
[ApiController]
[Route("api/appointments")]
public sealed class AppointmentsController(
    AppointmentService appointmentService,
    ITenantContext tenantContext) : ControllerBase
{
    /// <summary>
    /// Cria um agendamento (fluxo F1).
    /// </summary>
    /// <response code="201">Agendamento criado no estado Confirmed.</response>
    /// <response code="400">Pedido inválido segundo as regras de negócio.</response>
    /// <response code="409">O horário conflita com outro agendamento.</response>
    [HttpPost]
    [ProducesResponseType<AppointmentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // O tenant vem do contexto resolvido no servidor, nunca do corpo.
        if (!tenantContext.HasTenant)
        {
            return BadRequest(new ErrorResponse("Estabelecimento não identificado."));
        }

        var command = new CreateAppointmentCommand(
            tenantContext.TenantId!.Value,
            request.CustomerId,
            request.ServiceId,
            request.ProfessionalId,
            request.Date,
            request.Time,
            request.DurationMinutes);

        var result = await appointmentService.CreateAsync(command, cancellationToken);

        if (result.IsSuccess)
        {
            var body = AppointmentResponse.From(result.Appointment!);

            // Ainda nao existe endpoint de leitura por id; o Location aponta
            // para o recurso criado no proprio colecao.
            return Created($"/api/appointments/{body.Id}", body);
        }

        var failure = result.Failure!;

        return failure.Kind switch
        {
            AppointmentFailureKind.Conflict => Conflict(new ErrorResponse(failure.Message)),
            _ => BadRequest(new ErrorResponse(failure.Message))
        };
    }
}
