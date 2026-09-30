using BackAndre.Infrastructure.Data;
using BackAndre.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace BackAndre.Infrastructure.Repositories;

public class ServiceRepository(AppDbContext db)
{
    public async Task<List<Service>> GetActiveServicesAsync()
    {
        return await db.Services
            .AsNoTracking()
            .Where(service => service.IsActive)
            .OrderBy(service => service.Name)
            .ToListAsync();
    }

    public async Task<Service?> GetActiveByIdAsync(string id)
    {
        return await db.Services
            .SingleOrDefaultAsync(service => service.Id == id && service.IsActive);
    }

    public async Task SaveChangesAsync()
    {
        await db.SaveChangesAsync();
    }
}