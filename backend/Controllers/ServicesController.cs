using BackAndre.Data;
using BackAndre.DTOs.Common;
using BackAndre.DTOs.Services;
using BackAndre.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackAndre.Controllers;

[ApiController]
[Route("api/services")]
public class ServicesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<ServiceResponse>>> GetAll()
    {
        var services = await db.Services
            .AsNoTracking()
            .Where(service => service.IsActive)
            .OrderBy(service => service.Name)
            .Select(service => ToResponse(service))
            .ToListAsync();
        return Ok(services);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ServiceResponse>> Update(string id, UpdateServiceRequest request)
    {
        var service = await db.Services.SingleOrDefaultAsync(item => item.Id == id && item.IsActive);
        if (service is null)
        {
            return NotFound(new ErrorResponse("Serviço não encontrado."));
        }

        service.Name = request.Name.Trim();
        service.Description = request.Description.Trim();
        service.Price = request.Price;
        service.Duration = request.Duration;
        service.Highlight = request.Highlight;
        await db.SaveChangesAsync();
        return Ok(ToResponse(service));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var service = await db.Services.SingleOrDefaultAsync(item => item.Id == id && item.IsActive);
        if (service is null)
        {
            return NotFound(new ErrorResponse("Serviço não encontrado."));
        }

        service.IsActive = false;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static ServiceResponse ToResponse(Service service) =>
        new(service.Id, service.Name, service.Description, service.Price, service.Duration, service.Highlight);
}
