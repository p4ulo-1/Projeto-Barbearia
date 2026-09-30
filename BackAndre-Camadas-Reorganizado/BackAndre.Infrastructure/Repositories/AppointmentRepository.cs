using BackAndre.Infrastructure.Data;
using BackAndre.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace BackAndre.Infrastructure.Repositories;

public class AppointmentRepository(AppDbContext db)
{
    public async Task<List<Appointment>> GetUserAppointmentsAsync(string userId)
    {
        return await AppointmentQuery()
            .Where(appointment => appointment.UserId == userId)
            .OrderByDescending(appointment => appointment.Date)
            .ThenByDescending(appointment => appointment.Time)
            .ToListAsync();
    }

    public async Task<List<Appointment>> GetAllAsync()
    {
        return await AppointmentQuery()
            .OrderBy(appointment => appointment.Date)
            .ThenBy(appointment => appointment.Time)
            .ToListAsync();
    }

    public async Task<Appointment?> GetByIdAsync(string id)
    {
        return await db.Appointments.FindAsync(id);
    }

    public async Task<Appointment?> GetByIdWithDetailsAsync(string id)
    {
        return await db.Appointments
            .Include(item => item.User)
            .Include(item => item.Service)
            .Include(item => item.Barber)
            .SingleOrDefaultAsync(item => item.Id == id);
    }

    public async Task<List<Appointment>> GetBlockingAppointmentsAsync(
        string barberId,
        DateOnly date,
        string? ignoredAppointmentId)
    {
        return await db.Appointments
            .AsNoTracking()
            .Include(item => item.Service)
            .Where(item =>
                item.BarberId == barberId &&
                item.Date == date &&
                item.Status == Appointment.ConfirmedStatus &&
                item.Id != ignoredAppointmentId)
            .ToListAsync();
    }

    public async Task AddAsync(Appointment appointment)
    {
        await db.Appointments.AddAsync(appointment);
    }

    public async Task LoadRelationsAsync(Appointment appointment)
    {
        await db.Entry(appointment).Reference(item => item.User).LoadAsync();
        await db.Entry(appointment).Reference(item => item.Service).LoadAsync();
        await db.Entry(appointment).Reference(item => item.Barber).LoadAsync();
    }

    public void Remove(Appointment appointment)
    {
        db.Appointments.Remove(appointment);
    }

    public async Task SaveChangesAsync()
    {
        await db.SaveChangesAsync();
    }

    private IQueryable<Appointment> AppointmentQuery() => db.Appointments
        .AsNoTracking()
        .Include(appointment => appointment.User)
        .Include(appointment => appointment.Service)
        .Include(appointment => appointment.Barber);
}