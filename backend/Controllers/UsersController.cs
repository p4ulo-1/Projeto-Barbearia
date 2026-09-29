using System.Security.Claims;
using BackAndre.Data;
using BackAndre.DTOs.Common;
using BackAndre.DTOs.Users;
using BackAndre.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackAndre.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public class UsersController(AppDbContext db, IPasswordHasher<User> passwordHasher) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> GetMe()
    {
        var user = await FindCurrentUserAsync();
        return user is null
            ? Unauthorized(new ErrorResponse("Usuário do token não foi encontrado."))
            : Ok(ToResponse(user));
    }

    [HttpPut("me")]
    public async Task<ActionResult<UserResponse>> UpdateMe(UpdateUserRequest request)
    {
        var user = await FindCurrentUserAsync();
        if (user is null)
        {
            return Unauthorized(new ErrorResponse("Usuário do token não foi encontrado."));
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(item => item.Id != user.Id && item.Email == normalizedEmail))
        {
            return Conflict(new ErrorResponse("Já existe uma conta com este e-mail."));
        }

        user.Name = request.Name.Trim();
        user.Email = normalizedEmail;
        user.Phone = request.Phone.Trim();
        await db.SaveChangesAsync();
        return Ok(ToResponse(user));
    }

    [HttpPut("me/password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var user = await FindCurrentUserAsync();
        if (user is null)
        {
            return Unauthorized(new ErrorResponse("Usuário do token não foi encontrado."));
        }

        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword);
        if (result == PasswordVerificationResult.Failed)
        {
            return BadRequest(new ErrorResponse("Senha atual incorreta."));
        }

        user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("me")]
    public async Task<IActionResult> DeleteMe(DeleteAccountRequest request)
    {
        var user = await FindCurrentUserAsync();
        if (user is null)
        {
            return Unauthorized(new ErrorResponse("Usuário do token não foi encontrado."));
        }

        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            return BadRequest(new ErrorResponse("Senha incorreta."));
        }

        db.Users.Remove(user);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("clients")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<IEnumerable<ClientResponse>>> GetClients()
    {
        var clients = await db.Users
            .AsNoTracking()
            .Where(user => user.Role == BackAndre.Models.User.ClientRole)
            .OrderBy(user => user.Name)
            .Select(user => new ClientResponse(
                user.Id,
                user.Name,
                user.Email,
                user.Phone,
                user.Appointments.Count))
            .ToListAsync();

        return Ok(clients);
    }

    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    private Task<User?> FindCurrentUserAsync() =>
        db.Users.SingleOrDefaultAsync(user => user.Id == CurrentUserId);

    private static UserResponse ToResponse(User user) =>
        new(user.Id, user.Name, user.Email, user.Phone, user.Role);
}
