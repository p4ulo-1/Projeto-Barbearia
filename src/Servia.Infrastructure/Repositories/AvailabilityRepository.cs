using Microsoft.EntityFrameworkCore;
using Servia.Domain.Appointments;
using Servia.Domain.Team;
using Servia.Infrastructure.Persistence;

namespace Servia.Infrastructure.Repositories;

/// <summary>
/// Implementação EF Core de <see cref="IAvailabilityRepository"/> (C9).
///
/// O filtro global restringe ao tenant corrente; o parâmetro explícito
/// documenta a intenção e protege caso o filtro seja desativado.
/// </summary>
public sealed class AvailabilityRepository(AppDbContext context) : IAvailabilityRepository
{
    public async Task<Servia.Domain.Tenancy.Tenant?> FindTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default) =>
        await context.Tenants
            .Where(tenant => tenant.Id == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<Professional?> FindProfessionalAsync(
        Guid tenantId,
        Guid professionalId,
        CancellationToken cancellationToken = default) =>
        await context.Professionals
            .Where(professional => professional.TenantId == tenantId && professional.Id == professionalId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Professional>> ListActiveProfessionalsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default) =>
        await context.Professionals
            .Where(professional => professional.TenantId == tenantId && professional.IsActive)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Appointment>> GetConfirmedAppointmentsAsync(
        Guid tenantId,
        Guid professionalId,
        DateOnly date,
        CancellationToken cancellationToken = default) =>
        await context.Appointments
            .AsNoTracking()
            .Where(appointment => appointment.TenantId == tenantId
                && appointment.ProfessionalId == professionalId
                && appointment.Date == date
                && appointment.Status == AppointmentStatus.Confirmed)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ScheduleBlock>> GetBlocksAsync(
        Guid tenantId,
        Guid professionalId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var dayStart = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);

        return await context.ScheduleBlocks
            .Where(block => block.TenantId == tenantId
                && block.ProfessionalId == professionalId
                && block.Start < dayEnd
                && block.End > dayStart)
            .ToListAsync(cancellationToken);
    }
}
