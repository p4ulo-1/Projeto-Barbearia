using System.Security.Claims;
using BackAndre.Application.DTOs.Appointments;
using BackAndre.Application.DTOs.Common;
using BackAndre.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackAndre.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/appointments")]
public class AppointmentsController(AppointmentService appointmentService) : ControllerBase
{
    [HttpGet("availability")]
    [AllowAnonymous]
    public async Task<ActionResult<AvailabilityResponse>> GetAvailability(
        [FromQuery] string serviceId,
        [FromQuery] string barberId,
        [FromQuery] string date,
        [FromQuery] string? appointmentId = null)
    {
        var (response, error, statusCode) = await appointmentService.GetAvailabilityAsync(
            serviceId,
            barberId,
            date,
            appointmentId,
            User.Identity?.IsAuthenticated == true,
            CurrentUserId,
            User.IsInRole(BackAndre.Domain.Models.User.AdminRole));

        if (error is not null)
        {
            return StatusCode(statusCode, new ErrorResponse(error));
        }

        return Ok(response);
    }

    [HttpGet("me")]
    public async Task<ActionResult<IEnumerable<AppointmentResponse>>> GetMine()
    {
        if (CurrentUserId is null)
            return Unauthorized(new ErrorResponse("Usuário não autenticado."));

        var response = await appointmentService.GetMineAsync(CurrentUserId);
        return Ok(response);
    }

    [HttpGet]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<IEnumerable<AppointmentResponse>>> GetAll()
    {
        var response = await appointmentService.GetAllAsync();
        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<AppointmentResponse>> Create(CreateAppointmentRequest request)
    {
        if (CurrentUserId is null)
            return Unauthorized(new ErrorResponse("Usuário não autenticado."));

        var (response, error, statusCode) = await appointmentService.CreateAsync(CurrentUserId, request);
        if (error is not null)
        {
            return StatusCode(statusCode, new ErrorResponse(error));
        }

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPut("{id}/reschedule")]
    public async Task<ActionResult<AppointmentResponse>> Reschedule(
        string id,
        RescheduleAppointmentRequest request)
    {
        if (CurrentUserId is null)
            return Unauthorized(new ErrorResponse("Usuário não autenticado."));

        var (response, error, statusCode) = await appointmentService.RescheduleAsync(id, CurrentUserId, request);
        if (error is not null)
        {
            return StatusCode(statusCode, new ErrorResponse(error));
        }

        return Ok(response);
    }

    [HttpPatch("{id}/cancel")]
    public async Task<IActionResult> Cancel(string id)
    {
        if (CurrentUserId is null)
            return Unauthorized(new ErrorResponse("Usuário não autenticado."));

        var (success, error) = await appointmentService.CancelAsync(id, CurrentUserId);
        if (!success)
        {
            return NotFound(new ErrorResponse(error!));
        }

        return NoContent();
    }

    [HttpPatch("{id}/complete")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Complete(string id)
    {
        var (success, error) = await appointmentService.CompleteAsync(id);
        if (!success)
        {
            return NotFound(new ErrorResponse(error!));
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var (success, error) = await appointmentService.DeleteAsync(id);
        if (!success)
        {
            return NotFound(new ErrorResponse(error!));
        }

        return NoContent();
    }

    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);
}