using System.Globalization;
using BackAndre.Application.DTOs.Appointments;
using BackAndre.Domain.Models;
using BackAndre.Infrastructure.Repositories;

namespace BackAndre.Application.Services;

public class AppointmentService(
    AppointmentRepository appointmentRepository,
    ServiceRepository serviceRepository,
    BarberRepository barberRepository)
{
    public async Task<(AvailabilityResponse? Response, string? Error, int StatusCode)> GetAvailabilityAsync(
        string serviceId,
        string barberId,
        string date,
        string? appointmentId,
        bool isAuthenticated,
        string? currentUserId,
        bool isAdmin)
    {
        if (!DateOnly.TryParseExact(
                date,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsedDate))
        {
            return (null, "Use a data no formato yyyy-MM-dd.", 400);
        }

        var service = await serviceRepository.GetActiveByIdAsync(serviceId);
        if (service is null)
        {
            return (null, "Serviço inexistente ou indisponível.", 400);
        }

        var barber = await barberRepository.GetActiveByIdAsync(barberId);
        if (barber is null)
        {
            return (null, "Barbeiro inexistente ou indisponível.", 400);
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (parsedDate < today)
        {
            return (null, "A data não pode estar no passado.", 400);
        }

        string? ignoredAppointmentId = null;
        if (!string.IsNullOrWhiteSpace(appointmentId))
        {
            if (!isAuthenticated)
            {
                return (null, "É necessário estar autenticado para ignorar um agendamento.", 403);
            }

            var appointment = await appointmentRepository.GetByIdAsync(appointmentId);
            if (appointment is null)
            {
                return (null, "Agendamento não encontrado.", 404);
            }

            var canIgnore = appointment.UserId == currentUserId || isAdmin;
            if (!canIgnore)
            {
                return (null, "Você não pode ignorar este agendamento.", 403);
            }

            ignoredAppointmentId = appointment.Id;
        }

        if (!barber.Schedule.WorksOn(parsedDate))
        {
            return (new AvailabilityResponse(date, []), null, 200);
        }

        var blockingAppointments = await appointmentRepository.GetBlockingAppointmentsAsync(
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

        return (new AvailabilityResponse(date, slots.ToArray()), null, 200);
    }

    public async Task<IEnumerable<AppointmentResponse>> GetMineAsync(string userId)
    {
        var appointments = await appointmentRepository.GetUserAppointmentsAsync(userId);
        return appointments.Select(ToResponse);
    }

    public async Task<IEnumerable<AppointmentResponse>> GetAllAsync()
    {
        var appointments = await appointmentRepository.GetAllAsync();
        return appointments.Select(ToResponse);
    }

    public async Task<(AppointmentResponse? Response, string? Error, int StatusCode)> CreateAsync(
        string userId,
        CreateAppointmentRequest request)
    {
        if (!TryParseDateTime(request.Date, request.Time, out var date, out var time))
        {
            return (null, "Use data no formato yyyy-MM-dd e horário no formato HH:mm.", 400);
        }

        var validation = await ValidateSlotAsync(request.ServiceId, request.BarberId, date, time);
        if (validation.Error is not null)
        {
            return (null, validation.Error, validation.StatusCode);
        }

        var appointment = new Appointment(
            userId,
            validation.Service!.Id,
            validation.Barber!.Id,
            date,
            time);

        await appointmentRepository.AddAsync(appointment);
        await appointmentRepository.SaveChangesAsync();
        await appointmentRepository.LoadRelationsAsync(appointment);

        return (ToResponse(appointment), null, 201);
    }

    public async Task<(AppointmentResponse? Response, string? Error, int StatusCode)> RescheduleAsync(
        string id,
        string userId,
        RescheduleAppointmentRequest request)
    {
        var appointment = await appointmentRepository.GetByIdWithDetailsAsync(id);
        if (appointment is null || appointment.UserId != userId)
        {
            return (null, "Agendamento não encontrado.", 404);
        }

        if (!appointment.IsConfirmed)
        {
            return (null, "Somente agendamentos confirmados podem ser remarcados.", 400);
        }

        if (!TryParseDateTime(request.Date, request.Time, out var date, out var time))
        {
            return (null, "Use data no formato yyyy-MM-dd e horário no formato HH:mm.", 400);
        }

        var validation = await ValidateSlotAsync(
            appointment.ServiceId,
            appointment.BarberId,
            date,
            time,
            appointment.Id);
        if (validation.Error is not null)
        {
            return (null, validation.Error, validation.StatusCode);
        }

        // Chamada de método de domínio preservada
        appointment.Reschedule(date, time);
        await appointmentRepository.SaveChangesAsync();

        return (ToResponse(appointment), null, 200);
    }

    public async Task<(bool Success, string? Error)> CancelAsync(string id, string userId)
    {
        var appointment = await appointmentRepository.GetByIdAsync(id);
        if (appointment is null || appointment.UserId != userId)
        {
            return (false, "Agendamento não encontrado.");
        }

        // Chamada de método de domínio preservada
        appointment.Cancel();
        await appointmentRepository.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> CompleteAsync(string id)
    {
        var appointment = await appointmentRepository.GetByIdAsync(id);
        if (appointment is null)
        {
            return (false, "Agendamento não encontrado.");
        }

        // Chamada de método de domínio preservada
        appointment.Complete(DateTime.Now);
        await appointmentRepository.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeleteAsync(string id)
    {
        var appointment = await appointmentRepository.GetByIdAsync(id);
        if (appointment is null)
        {
            return (false, "Agendamento não encontrado.");
        }

        appointmentRepository.Remove(appointment);
        await appointmentRepository.SaveChangesAsync();
        return (true, null);
    }

    private async Task<SlotValidation> ValidateSlotAsync(
        string serviceId,
        string barberId,
        DateOnly date,
        TimeOnly time,
        string? ignoredAppointmentId = null)
    {
        var service = await serviceRepository.GetActiveByIdAsync(serviceId);
        if (service is null)
        {
            return SlotValidation.BadRequest("Serviço inexistente ou indisponível.");
        }

        var barber = await barberRepository.GetActiveByIdAsync(barberId);
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

        var existingAppointments = await appointmentRepository.GetBlockingAppointmentsAsync(
            barberId,
            date,
            ignoredAppointmentId);
        var overlaps = HasOverlap(existingAppointments, time, service.Duration);

        return overlaps
            ? SlotValidation.Conflict("Este horário conflita com outro agendamento.")
            : SlotValidation.Success(service, barber);
    }

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
            new(service, barber, null, 200);

        public static SlotValidation BadRequest(string error) =>
            new(null, null, error, 400);

        public static SlotValidation Conflict(string error) =>
            new(null, null, error, 409);
    }
}