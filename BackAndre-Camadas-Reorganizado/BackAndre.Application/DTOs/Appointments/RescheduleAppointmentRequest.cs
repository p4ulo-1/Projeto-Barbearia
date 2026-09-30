using System.ComponentModel.DataAnnotations;

namespace BackAndre.Application.DTOs.Appointments;

public class RescheduleAppointmentRequest
{
    [Required]
    public string Date { get; set; } = string.Empty;

    [Required]
    public string Time { get; set; } = string.Empty;
}
