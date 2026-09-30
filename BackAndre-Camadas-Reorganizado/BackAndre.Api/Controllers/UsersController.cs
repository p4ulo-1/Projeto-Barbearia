using System.Security.Claims;
using BackAndre.Application.DTOs.Common;
using BackAndre.Application.DTOs.Users;
using BackAndre.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackAndre.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public class UsersController(UserService userService) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> GetMe()
    {
        if (CurrentUserId is null)
        {
            return Unauthorized(new ErrorResponse("Usuário do token não foi encontrado."));
        }

        var user = await userService.GetByIdAsync(CurrentUserId);
        return user is null
            ? Unauthorized(new ErrorResponse("Usuário do token não foi encontrado."))
            : Ok(user);
    }

    [HttpPut("me")]
    public async Task<ActionResult<UserResponse>> UpdateMe(UpdateUserRequest request)
    {
        if (CurrentUserId is null)
        {
            return Unauthorized(new ErrorResponse("Usuário do token não foi encontrado."));
        }

        var (response, error, statusCode) = await userService.UpdateAsync(CurrentUserId, request);
        if (error is not null)
        {
            return StatusCode(statusCode, new ErrorResponse(error));
        }

        return Ok(response);
    }

    [HttpPut("me/password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        if (CurrentUserId is null)
        {
            return Unauthorized(new ErrorResponse("Usuário do token não foi encontrado."));
        }

        var (success, error, statusCode) = await userService.ChangePasswordAsync(CurrentUserId, request);
        if (!success)
        {
            return StatusCode(statusCode, new ErrorResponse(error!));
        }

        return NoContent();
    }

    [HttpDelete("me")]
    public async Task<IActionResult> DeleteMe(DeleteAccountRequest request)
    {
        if (CurrentUserId is null)
        {
            return Unauthorized(new ErrorResponse("Usuário do token não foi encontrado."));
        }

        var (success, error, statusCode) = await userService.DeleteAsync(CurrentUserId, request);
        if (!success)
        {
            return StatusCode(statusCode, new ErrorResponse(error!));
        }

        return NoContent();
    }

    [HttpGet("clients")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<IEnumerable<ClientResponse>>> GetClients()
    {
        var clients = await userService.GetClientsAsync();
        return Ok(clients);
    }

    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);
}
