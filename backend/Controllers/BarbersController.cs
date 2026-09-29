using BackAndre.Data;
using BackAndre.DTOs.Barbers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackAndre.Controllers;

[ApiController]
[Route("api/barbers")]
public class BarbersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<BarberResponse>>> GetAll()
    {
        var entities = await db.Barbers
            .AsNoTracking()
            .Where(barber => barber.IsActive)
            .OrderBy(barber => barber.Name)
            .ToListAsync();

        var barbers = entities.Select(barber => new BarberResponse(
                barber.Id,
                barber.Name,
                barber.Role,
                barber.Bio,
                barber.Schedule.WorkDayNumbers.ToArray(),
                barber.Schedule.StartTime.ToString("HH:mm"),
                barber.Schedule.EndTime.ToString("HH:mm")));

        return Ok(barbers);
    }
}
