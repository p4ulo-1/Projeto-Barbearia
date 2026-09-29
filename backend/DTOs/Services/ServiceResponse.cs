namespace BackAndre.DTOs.Services;

public record ServiceResponse(
    string Id,
    string Name,
    string Description,
    decimal Price,
    int Duration,
    bool Highlight);
