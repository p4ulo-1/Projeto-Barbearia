using BackAndre.Application.DTOs.Users;

namespace BackAndre.Application.DTOs.Auth;

public record AuthResponse(string Token, DateTime ExpiresAt, UserResponse User);
