namespace BackAndre.Application.DTOs.Barbers;

public record BarberResponse(
    string Id,
    string Name,
    string Role,
    string Bio,
    int[] WorkDays,
    string Start,
    string End);
