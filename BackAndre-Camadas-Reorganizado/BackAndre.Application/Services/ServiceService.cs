using BackAndre.Application.DTOs.Services;
using BackAndre.Domain.Models;
using BackAndre.Infrastructure.Repositories;

namespace BackAndre.Application.Services;

public class ServiceService(ServiceRepository serviceRepository)
{
    public async Task<IEnumerable<ServiceResponse>> GetAllActiveAsync()
    {
        var services = await serviceRepository.GetActiveServicesAsync();
        return services.Select(ToResponse);
    }

    public async Task<ServiceResponse?> UpdateAsync(string id, UpdateServiceRequest request)
    {
        var service = await serviceRepository.GetActiveByIdAsync(id);
        if (service is null)
        {
            return null;
        }

        service.Name = request.Name.Trim();
        service.Description = request.Description.Trim();
        service.Price = request.Price;
        service.Duration = request.Duration;
        service.Highlight = request.Highlight;

        await serviceRepository.SaveChangesAsync();
        return ToResponse(service);
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var service = await serviceRepository.GetActiveByIdAsync(id);
        if (service is null)
        {
            return false;
        }

        service.IsActive = false;
        await serviceRepository.SaveChangesAsync();
        return true;
    }

    private static ServiceResponse ToResponse(Service service) =>
        new(service.Id, service.Name, service.Description, service.Price, service.Duration, service.Highlight);
}