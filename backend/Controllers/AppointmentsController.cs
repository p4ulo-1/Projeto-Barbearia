using System.Globalization;
using System.Security.Claims;
using BackAndre.Data;
using BackAndre.DTOs.Appointments;
using BackAndre.DTOs.Common;
using BackAndre.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackAndre.Controllers;

[ApiController]
[Authorize]
[Route("api/appointments")]
public class AppointmentsController(AppDbContext db) : ControllerBase
{
    [HttpGet("availability")]
    [AllowAnonymous]
    public async Task<ActionResult<AvailabilityResponse>> GetAvailability(
        [FromQuery] string serviceId,
        [FromQuery] string barberId,
        [FromQuery] string date,
        [FromQuery] string? appointmentId = null)
    {
        if (!DateOnly.TryParseExact(
                date,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsedDate))
        {
            return BadRequest(new ErrorResponse("Use a data no formato yyyy-MM-dd."));
        }

        var service = await db.Services.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == serviceId && item.IsActive);
        if (service is null)
        {
            return BadRequest(new ErrorResponse("Serviço inexistente ou indisponível."));
        }

        var barber = await db.Barbers.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == barberId && item.IsActive);
        if (barber is null)
        {
            return BadRequest(new ErrorResponse("Barbeiro inexistente ou indisponível."));
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (parsedDate < today)
        {
            return BadRequest(new ErrorResponse("A data não pode estar no passado."));
        }

        string? ignoredAppointmentId = null;
        if (!string.IsNullOrWhiteSpace(appointmentId))
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new ErrorResponse("É necessário estar autenticado para ignorar um agendamento."));
            }

            var appointment = await db.Appointments.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == appointmentId);
            if (appointment is null)
            {
                return NotFound(new ErrorResponse("Agendamento não encontrado."));
            }

            var canIgnore = appointment.UserId == CurrentUserId ||
                User.IsInRole(BackAndre.Models.User.AdminRole);
            if (!canIgnore)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new ErrorResponse("Você não pode ignorar este agendamento."));
            }

            ignoredAppointmentId = appointment.Id;
        }

        if (!barber.Schedule.WorksOn(parsedDate))
        {
            return Ok(new AvailabilityResponse(date, []));
        }

        var blockingAppointments = await GetBlockingAppointmentsAsync(
            barberId,
            parsedDate,
            ignoredAppointmentId);
        var slots = new List<string>();
        var minimumTime = parsedDate == today
            ? TimeOnly.FromDateTime(DateTime.Now.AddMinutes(30))
            : (TimeOnly?)null;

        for (var time = barber.Schedule.StartTime;
             barber.Schedule.FitsWithinSchedule(time, service.Duration);
             time = time.AddMinutes(30))
        {
            if (minimumTime.HasValue && time <= minimumTime.Value)
            {
                continue;
            }

            if (!HasOverlap(blockingAppointments, time, service.Duration))
            {
                slots.Add(time.ToString("HH:mm", CultureInfo.InvariantCulture));
            }
        }

        return Ok(new AvailabilityResponse(date, slots.ToArray()));
    }

    [HttpGet("me")]
    public async Task<ActionResult<IEnumerable<AppointmentResponse>>> GetMine()
    {
        var userId = CurrentUserId;
        var appointments = await AppointmentQuery()
            .Where(appointment => appointment.UserId == userId)
            .OrderByDescending(appointment => appointment.Date)
            .ThenByDescending(appointment => appointment.Time)
            .ToListAsync();

        return Ok(appointments.Select(ToResponse));
    }

    [HttpGet]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<IEnumerable<AppointmentResponse>>> GetAll()
    {
        var appointments = await AppointmentQuery()
            .OrderBy(appointment => appointment.Date)
            .ThenBy(appointment => appointment.Time)
            .ToListAsync();

        return Ok(appointments.Select(ToResponse));
    }

    [HttpPost]
    public async Task<ActionResult<AppointmentResponse>> Create(CreateAppointmentRequest request)
    {
        if (!TryParseDateTime(request.Date, request.Time, out var date, out var time))
        {
            return BadRequest(new ErrorResponse("Use data no formato yyyy-MM-dd e horário no formato HH:mm."));
        }

        var validation = await ValidateSlotAsync(request.ServiceId, request.BarberId, date, time);
        if (validation.Error is not null)
        {
            return StatusCode(validation.StatusCode, new ErrorResponse(validation.Error));
        }

        var appointment = new Appointment(
            CurrentUserId!,
            validation.Service!.Id,
            validation.Barber!.Id,
            date,
            time);

        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
        await LoadRelationsAsync(appointment);

        return StatusCode(StatusCodes.Status201Created, ToResponse(appointment));
    }

    [HttpPut("{id}/reschedule")]
    public async Task<ActionResult<AppointmentResponse>> Reschedule(
        string id,
        RescheduleAppointmentRequest request)
    {
        var appointment = await db.Appointments
            .Include(item => item.User)
            .Include(item => item.Service)
            .Include(item => item.Barber)
            .SingleOrDefaultAsync(item => item.Id == id);
        if (appointment is null || appointment.UserId != CurrentUserId)
        {
            return NotFound(new ErrorResponse("Agendamento não encontrado."));
        }

        if (!appointment.IsConfirmed)
        {
            return BadRequest(new ErrorResponse("Somente agendamentos confirmados podem ser remarcados."));
        }

        if (!TryParseDateTime(request.Date, request.Time, out var date, out var time))
        {
            return BadRequest(new ErrorResponse("Use data no formato yyyy-MM-dd e horário no formato HH:mm."));
        }

        var validation = await ValidateSlotAsync(
            appointment.ServiceId,
            appointment.BarberId,
            date,
            time,
            appointment.Id);
        if (validation.Error is not null)
        {
            return StatusCode(validation.StatusCode, new ErrorResponse(validation.Error));
        }

        appointment.Reschedule(date, time);
        await db.SaveChangesAsync();
        return Ok(ToResponse(appointment));
    }

    [HttpPatch("{id}/cancel")]
    public async Task<IActionResult> Cancel(string id)
    {
        var appointment = await db.Appointments.SingleOrDefaultAsync(item => item.Id == id);
        if (appointment is null || appointment.UserId != CurrentUserId)
        {
            return NotFound(new ErrorResponse("Agendamento não encontrado."));
        }

        appointment.Cancel();
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{id}/complete")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Complete(string id)
    {
        var appointment = await db.Appointments.SingleOrDefaultAsync(item => item.Id == id);
        if (appointment is null)
        {
            return NotFound(new ErrorResponse("Agendamento não encontrado."));
        }

        appointment.Complete(DateTime.Now);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var appointment = await db.Appointments.FindAsync(id);
        if (appointment is null)
        {
            return NotFound(new ErrorResponse("Agendamento não encontrado."));
        }

        db.Appointments.Remove(appointment);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    private IQueryable<Appointment> AppointmentQuery() => db.Appointments
        .AsNoTracking()
        .Include(appointment => appointment.User)
        .Include(appointment => appointment.Service)
        .Include(appointment => appointment.Barber);

    private async Task LoadRelationsAsync(Appointment appointment)
    {
        await db.Entry(appointment).Reference(item => item.User).LoadAsync();
        await db.Entry(appointment).Reference(item => item.Service).LoadAsync();
        await db.Entry(appointment).Reference(item => item.Barber).LoadAsync();
    }

    private async Task<SlotValidation> ValidateSlotAsync(
        string serviceId,
        string barberId,
        DateOnly date,
        TimeOnly time,
        string? ignoredAppointmentId = null)
    {
        var service = await db.Services.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == serviceId && item.IsActive);
        if (service is null)
        {
            return SlotValidation.BadRequest("Serviço inexistente ou indisponível.");
        }

        var barber = await db.Barbers.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == barberId && item.IsActive);
        if (barber is null)
        {
            return SlotValidation.BadRequest("Barbeiro inexistente ou indisponível.");
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (date < today)
        {
            return SlotValidation.BadRequest("A data não pode estar no passado.");
        }

        if (!barber.Schedule.WorksOn(date))
        {
            return SlotValidation.BadRequest("O barbeiro não trabalha na data escolhida.");
        }

        if (!barber.Schedule.FitsWithinSchedule(time, service.Duration))
        {
            return SlotValidation.BadRequest("O serviço não cabe no expediente do barbeiro.");
        }

        if (date == today && time <= TimeOnly.FromDateTime(DateTime.Now.AddMinutes(30)))
        {
            return SlotValidation.BadRequest("Escolha um horário com pelo menos 30 minutos de antecedência.");
        }

        var existingAppointments = await GetBlockingAppointmentsAsync(
            barberId,
            date,
            ignoredAppointmentId);
        var overlaps = HasOverlap(existingAppointments, time, service.Duration);

        return overlaps
            ? SlotValidation.Conflict("Este horário conflita com outro agendamento.")
            : SlotValidation.Success(service, barber);
    }

    private Task<List<Appointment>> GetBlockingAppointmentsAsync(
        string barberId,
        DateOnly date,
        string? ignoredAppointmentId) =>
        db.Appointments
            .AsNoTracking()
            .Include(item => item.Service)
            .Where(item =>
                item.BarberId == barberId &&
                item.Date == date &&
                item.Status == Appointment.ConfirmedStatus &&
                item.Id != ignoredAppointmentId)
            .ToListAsync();

    private static bool HasOverlap(
        IEnumerable<Appointment> appointments,
        TimeOnly time,
        int duration)
    {
        var endTime = time.AddMinutes(duration);
        return appointments.Any(existing =>
        {
            var existingEnd = existing.Time.AddMinutes(existing.Service.Duration);
            return time < existingEnd && endTime > existing.Time;
        });
    }

    private static bool TryParseDateTime(
        string dateText,
        string timeText,
        out DateOnly date,
        out TimeOnly time)
    {
        var validDate = DateOnly.TryParseExact(
            dateText,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);
        var validTime = TimeOnly.TryParseExact(
            timeText,
            "HH:mm",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out time);
        return validDate && validTime;
    }

    private static AppointmentResponse ToResponse(Appointment appointment) => new(
        appointment.Id,
        appointment.UserId,
        appointment.User.Name,
        appointment.ServiceId,
        appointment.Service.Name,
        appointment.BarberId,
        appointment.Barber.Name,
        appointment.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        appointment.Time.ToString("HH:mm", CultureInfo.InvariantCulture),
        appointment.Status,
        appointment.CreatedAt);

    private record SlotValidation(
        Service? Service,
        Barber? Barber,
        string? Error,
        int StatusCode)
    {
        public static SlotValidation Success(Service service, Barber barber) =>
            new(service, barber, null, StatusCodes.Status200OK);

        public static SlotValidation BadRequest(string error) =>
            new(null, null, error, StatusCodes.Status400BadRequest);

        public static SlotValidation Conflict(string error) =>
            new(null, null, error, StatusCodes.Status409Conflict);
    }
}
