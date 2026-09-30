using System.ComponentModel.DataAnnotations;

namespace BackAndre.Application.DTOs.Users;

public class DeleteAccountRequest
{
    [Required]
    public string Password { get; set; } = string.Empty;
}
