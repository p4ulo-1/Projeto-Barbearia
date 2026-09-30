using BackAndre.Application.DTOs.Auth;
using BackAndre.Application.DTOs.Users;
using BackAndre.Domain.Models;
using BackAndre.Infrastructure.Repositories;
using BackAndre.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;

namespace BackAndre.Application.Services;

public class AuthService(
    UserRepository userRepository,
    IPasswordHasher<User> passwordHasher,
    JwtTokenService jwtTokenService)
{
    public async Task<(AuthResponse? Response, string? ErrorMessage)> RegisterAsync(RegisterRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        if (await userRepository.ExistsByEmailAsync(normalizedEmail))
        {
            return (null, "Já existe uma conta com este e-mail.");
        }

        var user = new User
        {
            Name = request.Name.Trim(),
            Email = normalizedEmail,
            Phone = request.Phone.Trim(),
            Role = User.ClientRole
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        await userRepository.AddAsync(user);
        await userRepository.SaveChangesAsync();

        return (CreateAuthResponse(user), null);
    }

    public async Task<(AuthResponse? Response, string? ErrorMessage)> LoginAsync(LoginRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await userRepository.GetByEmailAsync(normalizedEmail);
        if (user is null)
        {
            return (null, "E-mail ou senha incorretos.");
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return (null, "E-mail ou senha incorretos.");
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
            await userRepository.SaveChangesAsync();
        }

        return (CreateAuthResponse(user), null);
    }

    private AuthResponse CreateAuthResponse(User user)
    {
        var token = jwtTokenService.Create(user);
        return new AuthResponse(
            token.Token,
            token.ExpiresAt,
            new UserResponse(user.Id, user.Name, user.Email, user.Phone, user.Role));
    }
}