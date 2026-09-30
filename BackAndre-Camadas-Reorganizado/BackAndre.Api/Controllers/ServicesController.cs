using BackAndre.Application.DTOs.Common;
using BackAndre.Application.DTOs.Services;
using BackAndre.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BackAndre.Api.Controllers;

[ApiController]
[Route("api/services")]
public class ServicesController(ServiceService serviceService) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<ServiceResponse>>> GetAll()
    {
        var services = await serviceService.GetAllActiveAsync();
        return Ok(services);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ServiceResponse>> Update(string id, UpdateServiceRequest request)
    {
        var response = await serviceService.UpdateAsync(id, request);
        if (response is null)
        {
            return NotFound(new ErrorResponse("Serviço não encontrado."));
        }

        return Ok(response);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var success = await serviceService.DeleteAsync(id);
        if (!success)
        {
            return NotFound(new ErrorResponse("Serviço não encontrado."));
        }

        return NoContent();
    }
}