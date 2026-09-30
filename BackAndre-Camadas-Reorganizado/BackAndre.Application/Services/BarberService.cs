using BackAndre.Application.DTOs.Barbers;
using BackAndre.Domain.Models;
using BackAndre.Infrastructure.Repositories;

namespace BackAndre.Application.Services;

public class BarberService(BarberRepository barberRepository)
{
    public async Task<IEnumerable<BarberResponse>> GetAllActiveAsync()
    {
        var barbers = await barberRepository.GetActiveBarbersAsync();
        return barbers.Select(ToResponse);
    }

    private static BarberResponse ToResponse(Barber barber) =>
        new(
            barber.Id,
            barber.Name,
            barber.Role,
            barber.Bio,
            barber.Schedule.WorkDayNumbers.ToArray(),
            barber.Schedule.StartTime.ToString("HH:mm"),
            barber.Schedule.EndTime.ToString("HH:mm"));
}