using System.ComponentModel.DataAnnotations;

namespace BackAndre.Application.DTOs.Users;

public class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "A nova senha deve ter pelo menos 6 caracteres.")]
    public string NewPassword { get; set; } = string.Empty;
}
