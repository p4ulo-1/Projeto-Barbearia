using BackAndre.Infrastructure.Data;
using BackAndre.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace BackAndre.Infrastructure.Repositories;

public class UserRepository(AppDbContext db)
{
    public async Task<User?> GetByIdAsync(string id)
    {
        return await db.Users.SingleOrDefaultAsync(user => user.Id == id);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await db.Users.SingleOrDefaultAsync(user => user.Email == email);
    }

    public async Task<bool> ExistsByEmailAsync(string email)
    {
        return await db.Users.AnyAsync(user => user.Email == email);
    }

    public async Task<bool> ExistsByEmailExceptUserAsync(string email, string userId)
    {
        return await db.Users.AnyAsync(user => user.Id != userId && user.Email == email);
    }

    public async Task<List<User>> GetClientsWithAppointmentCountAsync()
    {
        return await db.Users
            .AsNoTracking()
            .Include(u => u.Appointments)
            .Where(user => user.Role == User.ClientRole)
            .OrderBy(user => user.Name)
            .ToListAsync();
    }

    public async Task AddAsync(User user)
    {
        await db.Users.AddAsync(user);
    }

    public void Remove(User user)
    {
        db.Users.Remove(user);
    }

    public async Task SaveChangesAsync()
    {
        await db.SaveChangesAsync();
    }
}