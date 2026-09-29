namespace BackAndre.Models;

public class Barber
{
    private Barber()
    {
    }

    public Barber(string id, string name, string role, string bio, BarberSchedule schedule)
    {
        Id = id;
        Name = name;
        Role = role;
        Bio = bio;
        Schedule = schedule;
    }

    public string Id { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Role { get; private set; } = string.Empty;
    public string Bio { get; private set; } = string.Empty;
    public BarberSchedule Schedule { get; private set; } = null!;
    public bool IsActive { get; private set; } = true;
    public ICollection<Appointment> Appointments { get; private set; } = [];
}
