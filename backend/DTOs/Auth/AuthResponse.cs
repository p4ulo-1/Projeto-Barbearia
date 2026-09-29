using BackAndre.DTOs.Users;

namespace BackAndre.DTOs.Auth;

public record AuthResponse(string Token, DateTime ExpiresAt, UserResponse User);
