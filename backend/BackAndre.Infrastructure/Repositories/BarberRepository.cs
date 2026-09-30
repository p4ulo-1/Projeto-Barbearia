using BackAndre.Infrastructure.Data;
using BackAndre.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace BackAndre.Infrastructure.Repositories;

public class BarberRepository(AppDbContext db)
{
    public async Task<List<Barber>> GetActiveBarbersAsync()
    {
        return await db.Barbers
            .AsNoTracking()
            .Where(barber => barber.IsActive)
            .OrderBy(barber => barber.Name)
            .ToListAsync();
    }

    public async Task<Barber?> GetActiveByIdAsync(string id)
    {
        return await db.Barbers
            .AsNoTracking()
            .SingleOrDefaultAsync(barber => barber.Id == id && barber.IsActive);
    }
}
