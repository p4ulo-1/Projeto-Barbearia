using System.ComponentModel.DataAnnotations;

namespace BackAndre.DTOs.Users;

public class DeleteAccountRequest
{
    [Required]
    public string Password { get; set; } = string.Empty;
}
