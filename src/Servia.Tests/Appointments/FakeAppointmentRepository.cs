using Servia.Domain.Appointments;

namespace Servia.Tests.Appointments;

/// <summary>
/// Dublê de repositório em memória.
///
/// Permite testar a regra de não sobreposição sem banco de dados. Reproduz o
/// comportamento essencial do repositório real: filtra por tenant, profissional
/// e data, e exclui agendamentos cancelados.
/// </summary>
internal sealed class FakeAppointmentRepository : IAppointmentRepository
{
    private readonly List<Appointment> _appointments = [];

    public int SaveCount { get; private set; }

    public IReadOnlyList<Appointment> All => _appointments;

    public void Seed(Appointment appointment) => _appointments.Add(appointment);

    public Task<IReadOnlyList<Appointment>> GetActiveByProfessionalAndDateAsync(
        Guid tenantId,
        Guid professionalId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Appointment> result = _appointments
            .Where(a => a.TenantId == tenantId
                        && a.ProfessionalId == professionalId
                        && a.Date == date
                        && a.IsConfirmed)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<Appointment?> GetByIdAsync(
        Guid tenantId,
        Guid appointmentId,
        CancellationToken cancellationToken = default)
    {
        var found = _appointments.FirstOrDefault(a =>
            a.TenantId == tenantId && a.Id == appointmentId);

        return Task.FromResult(found);
    }

    public Task AddAsync(Appointment appointment, CancellationToken cancellationToken = default)
    {
        _appointments.Add(appointment);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
