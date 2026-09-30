namespace BackAndre.Application.DTOs.Common;

public record ErrorResponse(
    string Message,
    IDictionary<string, string[]>? Errors = null,
    string? TraceId = null);
