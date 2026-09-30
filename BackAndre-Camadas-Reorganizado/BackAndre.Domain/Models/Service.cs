namespace BackAndre.Domain.Models;

public class Service
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Duration { get; set; }
    public bool Highlight { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<Appointment> Appointments { get; set; } = [];
}
