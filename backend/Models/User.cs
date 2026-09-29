namespace BackAndre.Models;

public class User
{
    public const string ClientRole = "client";
    public const string AdminRole = "admin";

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = ClientRole;
    public ICollection<Appointment> Appointments { get; set; } = [];
}
