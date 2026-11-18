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
    private readonly Dictionary<Guid, Servia.Domain.Catalog.Service> _services = [];

    public int SaveCount { get; private set; }

    public IReadOnlyList<Appointment> All => _appointments;

    public void Seed(Appointment appointment) => _appointments.Add(appointment);

    /// <summary>
    /// Semeia o serviço sob um identificador explícito.
    ///
    /// O comando de criação usa um identificador fixo; o dublê precisa
    /// responder por ele, e não pelo identificador gerado na construção.
    /// </summary>
    public void SeedService(Servia.Domain.Catalog.Service service, Guid? id = null) =>
        _services[id ?? service.Id] = service;

    public Task<Servia.Domain.Catalog.Service?> FindServiceAsync(
        Guid tenantId,
        Guid serviceId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(_services.TryGetValue(serviceId, out var service)
            && service.TenantId == tenantId
                ? service
                : null);

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

    /// <summary>Clientes adicionados na mesma transação (C6).</summary>
    public List<Servia.Domain.Customers.Customer> AddedCustomers { get; } = [];

    public Task AddCustomerAsync(
        Servia.Domain.Customers.Customer customer,
        CancellationToken cancellationToken = default)
    {
        AddedCustomers.Add(customer);
        return Task.CompletedTask;
    }

    public Task<T> ExecuteSerializableAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default) =>
        operation(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
