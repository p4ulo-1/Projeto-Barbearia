using BackAndre.Application.DTOs.Auth;
using BackAndre.Application.DTOs.Common;
using BackAndre.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace BackAndre.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService authService) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var (response, error) = await authService.RegisterAsync(request);
        if (error is not null)
        {
            return Conflict(new ErrorResponse(error));
        }

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var (response, error) = await authService.LoginAsync(request);
        if (error is not null)
        {
            return Unauthorized(new ErrorResponse(error));
        }

        return Ok(response);
    }
}