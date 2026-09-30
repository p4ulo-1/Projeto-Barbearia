using BackAndre.Application.DTOs.Barbers;
using BackAndre.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace BackAndre.Api.Controllers;

[ApiController]
[Route("api/barbers")]
public class BarbersController(BarberService barberService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<BarberResponse>>> GetAll()
    {
        var barbers = await barberService.GetAllActiveAsync();
        return Ok(barbers);
    }
}