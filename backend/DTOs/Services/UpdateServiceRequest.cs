using System.ComponentModel.DataAnnotations;

namespace BackAndre.DTOs.Services;

public class UpdateServiceRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(500, MinimumLength = 5)]
    public string Description { get; set; } = string.Empty;

    [Range(0.01, 10000)]
    public decimal Price { get; set; }

    [Range(15, 480)]
    public int Duration { get; set; }

    public bool Highlight { get; set; }
}
