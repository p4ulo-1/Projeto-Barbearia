using BackAndre.Application.DTOs.Users;
using BackAndre.Domain.Models;
using BackAndre.Infrastructure.Repositories;
using Microsoft.AspNetCore.Identity;

namespace BackAndre.Application.Services;

public class UserService(
    UserRepository userRepository,
    IPasswordHasher<User> passwordHasher)
{
    public async Task<UserResponse?> GetByIdAsync(string userId)
    {
        var user = await userRepository.GetByIdAsync(userId);
        return user is null ? null : ToResponse(user);
    }

    public async Task<(UserResponse? Response, string? Error, int StatusCode)> UpdateAsync(
        string userId,
        UpdateUserRequest request)
    {
        var user = await userRepository.GetByIdAsync(userId);
        if (user is null)
        {
            return (null, "Usuário do token não foi encontrado.", 401);
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        if (await userRepository.ExistsByEmailExceptUserAsync(normalizedEmail, user.Id))
        {
            return (null, "Já existe uma conta com este e-mail.", 409);
        }

        user.Name = request.Name.Trim();
        user.Email = normalizedEmail;
        user.Phone = request.Phone.Trim();
        await userRepository.SaveChangesAsync();

        return (ToResponse(user), null, 200);
    }

    public async Task<(bool Success, string? Error, int StatusCode)> ChangePasswordAsync(
        string userId,
        ChangePasswordRequest request)
    {
        var user = await userRepository.GetByIdAsync(userId);
        if (user is null)
        {
            return (false, "Usuário do token não foi encontrado.", 401);
        }

        var verification = passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.CurrentPassword);

        if (verification == PasswordVerificationResult.Failed)
        {
            return (false, "Senha atual incorreta.", 400);
        }

        user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
        await userRepository.SaveChangesAsync();
        return (true, null, 204);
    }

    public async Task<(bool Success, string? Error, int StatusCode)> DeleteAsync(
        string userId,
        DeleteAccountRequest request)
    {
        var user = await userRepository.GetByIdAsync(userId);
        if (user is null)
        {
            return (false, "Usuário do token não foi encontrado.", 401);
        }

        var verification = passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.Password);

        if (verification == PasswordVerificationResult.Failed)
        {
            return (false, "Senha incorreta.", 400);
        }

        userRepository.Remove(user);
        await userRepository.SaveChangesAsync();
        return (true, null, 204);
    }

    public async Task<IEnumerable<ClientResponse>> GetClientsAsync()
    {
        var clients = await userRepository.GetClientsWithAppointmentCountAsync();
        return clients.Select(user => new ClientResponse(
            user.Id,
            user.Name,
            user.Email,
            user.Phone,
            user.Appointments.Count));
    }

    private static UserResponse ToResponse(User user) =>
        new(user.Id, user.Name, user.Email, user.Phone, user.Role);
}
