namespace BackAndre.DTOs.Appointments;

public record AppointmentResponse(
    string Id,
    string UserId,
    string UserName,
    string ServiceId,
    string ServiceName,
    string BarberId,
    string BarberName,
    string Date,
    string Time,
    string Status,
    DateTime CreatedAt);
