namespace BackAndre.Application.DTOs.Users;

public record ClientResponse(string Id, string Name, string Email, string Phone, int AppointmentCount);
