namespace Servia.Domain.Appointments;

/// <summary>
/// Leitura necessária ao cálculo de disponibilidade (C9).
///
/// Separado do repositório de agendamento porque a disponibilidade depende
/// de quatro fontes: o serviço solicitado, o vínculo profissional-serviço,
/// a jornada do profissional, os bloqueios e os agendamentos ativos.
/// Todas as consultas são escopadas por tenant.
/// </summary>
public interface IAvailabilityRepository
{
    /// <summary>Estabelecimento do tenant (inclui o expediente).</summary>
    Task<Tenancy.Tenant?> FindTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Profissional do tenant, ou nulo.</summary>
    Task<Team.Professional?> FindProfessionalAsync(
        Guid tenantId,
        Guid professionalId,
        CancellationToken cancellationToken = default);

    /// <summary>Serviço do tenant, ou nulo.</summary>
    Task<Catalog.Service?> FindServiceAsync(
        Guid tenantId,
        Guid serviceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Indica se existe vínculo entre o profissional e o serviço no tenant.
    ///
    /// É o que autoriza o profissional a receber agendamentos daquele serviço.
    /// Sem o vínculo, o horário não pode ser oferecido nem reservado.
    /// </summary>
    Task<bool> IsServiceLinkedAsync(
        Guid tenantId,
        Guid professionalId,
        Guid serviceId,
        CancellationToken cancellationToken = default);

    /// <summary>Profissionais ativos do tenant.</summary>
    Task<IReadOnlyList<Team.Professional>> ListActiveProfessionalsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Agendamentos confirmados do profissional na data.</summary>
    Task<IReadOnlyList<Appointment>> GetConfirmedAppointmentsAsync(
        Guid tenantId,
        Guid professionalId,
        DateOnly date,
        CancellationToken cancellationToken = default);

    /// <summary>Bloqueios do profissional que tocam o dia local da agenda.</summary>
    Task<IReadOnlyList<Team.ScheduleBlock>> GetBlocksAsync(
        Guid tenantId,
        Guid professionalId,
        DateOnly date,
        CancellationToken cancellationToken = default);
}
