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

    public async Task AddCustomerAsync(
        Servia.Domain.Customers.Customer customer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(customer);

        await context.Customers.AddAsync(customer, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);

    public async Task<T> ExecuteSerializableAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        // Nivel serializavel: o PostgreSQL aborta uma das transacoes
        // concorrentes quando detecta que o resultado seria inconsistente.
        await using var transaction = await context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);

        var result = await operation(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return result;
    }
}
