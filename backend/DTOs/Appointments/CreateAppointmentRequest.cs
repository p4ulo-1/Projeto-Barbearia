using System.ComponentModel.DataAnnotations;

namespace BackAndre.DTOs.Appointments;

public class CreateAppointmentRequest
{
    [Required]
    public string ServiceId { get; set; } = string.Empty;

    [Required]
    public string BarberId { get; set; } = string.Empty;

    [Required]
    public string Date { get; set; } = string.Empty;

    [Required]
    public string Time { get; set; } = string.Empty;
}
