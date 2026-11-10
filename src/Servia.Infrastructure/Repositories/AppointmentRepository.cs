using Microsoft.EntityFrameworkCore;
using Servia.Domain.Appointments;
using Servia.Infrastructure.Persistence;

namespace Servia.Infrastructure.Repositories;

/// <summary>
/// Implementação EF Core de <see cref="IAppointmentRepository"/>.
///
/// Toda consulta é escopada por tenant: nenhum método atravessa a fronteira
/// de isolamento (ADR-016). O filtro por tenant é aplicado explicitamente na
/// consulta, e não apenas confiado ao chamador.
/// </summary>
public sealed class AppointmentRepository(AppDbContext context) : IAppointmentRepository
{
    public async Task<IReadOnlyList<Appointment>> GetActiveByProfessionalAndDateAsync(
        Guid tenantId,
        Guid professionalId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        return await context.Appointments
            .AsNoTracking()
            .Where(appointment =>
                appointment.TenantId == tenantId
                && appointment.ProfessionalId == professionalId
                && appointment.Date == date
                && appointment.Status == AppointmentStatus.Confirmed)
            .ToListAsync(cancellationToken);
    }

    public async Task<Appointment?> GetByIdAsync(
        Guid tenantId,
        Guid appointmentId,
        CancellationToken cancellationToken = default)
    {
        return await context.Appointments
            .FirstOrDefaultAsync(
                appointment => appointment.TenantId == tenantId
                               && appointment.Id == appointmentId,
                cancellationToken);
    }

    public async Task AddAsync(Appointment appointment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(appointment);

        await context.Appointments.AddAsync(appointment, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
