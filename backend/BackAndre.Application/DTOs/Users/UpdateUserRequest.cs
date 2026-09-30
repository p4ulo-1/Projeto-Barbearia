using System.ComponentModel.DataAnnotations;

namespace BackAndre.Application.DTOs.Users;

public class UpdateUserRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(30, MinimumLength = 8)]
    public string Phone { get; set; } = string.Empty;
}
