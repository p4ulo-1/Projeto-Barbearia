using Servia.Domain.Appointments;
using Servia.Domain.Catalog;
using Servia.Domain.Team;
using Servia.Domain.Tenancy;

namespace Servia.Tests.Appointments;

/// <summary>
/// Adaptador de <see cref="IAvailabilityRepository"/> sobre o dublê de
/// agendamentos, para os testes unitários de <c>AppointmentService</c>.
///
/// A criação passou a depender de <c>AvailabilityService</c>: a mesma regra
/// que a consulta usa. Em vez de duplicar um segundo dublê, este adaptador
/// reaproveita os dados já semeados no <see cref="FakeAppointmentRepository"/>
/// e acrescenta o que só a disponibilidade consulta — profissional, serviço
/// e vínculo.
///
/// O profissional devolvido tem jornada de segunda a domingo, 08:00–18:00, e
/// todo serviço é considerado vinculado. Assim os testes de sobreposição
/// medem exatamente o que pretendem: o conflito de horário. A identidade do
/// profissional não participa da regra de disponibilidade — ela filtra por
/// tenant, serviço, jornada, bloqueio e conflito.
/// </summary>
internal sealed class LinkedAvailabilityRepository(FakeAppointmentRepository appointments)
    : IAvailabilityRepository
{
    private readonly Dictionary<Guid, Professional> _professionals = [];

    public Task<Tenant?> FindTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<Tenant?>(null);

    public Task<Professional?> FindProfessionalAsync(
        Guid tenantId,
        Guid professionalId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<Professional?>(EnsureProfessional(tenantId, professionalId));

    public Task<Service?> FindServiceAsync(
        Guid tenantId,
        Guid serviceId,
        CancellationToken cancellationToken = default) =>
        appointments.FindServiceAsync(tenantId, serviceId, cancellationToken);

    public Task<bool> IsServiceLinkedAsync(
        Guid tenantId,
        Guid professionalId,
        Guid serviceId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public Task<IReadOnlyList<Professional>> ListActiveProfessionalsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Professional>>(_professionals.Values.ToList());

    public Task<IReadOnlyList<Appointment>> GetConfirmedAppointmentsAsync(
        Guid tenantId,
        Guid professionalId,
        DateOnly date,
        CancellationToken cancellationToken = default) =>
        appointments.GetActiveByProfessionalAndDateAsync(
            tenantId, professionalId, date, cancellationToken);

    public Task<IReadOnlyList<ScheduleBlock>> GetBlocksAsync(
        Guid tenantId,
        Guid professionalId,
        DateOnly date,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ScheduleBlock>>([]);

    /// <summary>
    /// Profissional de jornada ampla, criado sob demanda e reutilizado por
    /// tenant. A jornada cobre todos os dias e o dia inteiro para que os
    /// testes de sobreposição não esbarrem em "dia sem trabalho" nem em
    /// "fora do expediente" — inclusive no cenário que cruza a meia-noite.
    /// </summary>
    private Professional EnsureProfessional(Guid tenantId, Guid professionalId)
    {
        if (_professionals.TryGetValue(professionalId, out var existing))
        {
            return existing;
        }

        var schedule = WorkSchedule.Create(
            Enum.GetValues<DayOfWeek>(),
            new TimeOnly(0, 0),
            new TimeOnly(23, 59));

        var professional = Professional.Create(tenantId, "Profissional", schedule);

        _professionals[professionalId] = professional;

        return professional;
    }
}