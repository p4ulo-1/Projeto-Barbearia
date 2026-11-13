namespace Servia.Domain.Appointments;

/// <summary>
/// Leitura necessária ao cálculo de disponibilidade (C9).
///
/// Separado do repositório de agendamento porque a disponibilidade depende
/// de três fontes: jornada do profissional, bloqueios e agendamentos ativos.
/// Todas as consultas são escopadas por tenant.
/// </summary>
public interface IAvailabilityRepository
{
    /// <summary>Estabelecimento do tenant (inclui o expediente).</summary>
    Task<Tenancy.Tenant?> FindTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Profissional ativo do tenant, ou nulo.</summary>
    Task<Team.Professional?> FindProfessionalAsync(
        Guid tenantId,
        Guid professionalId,
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

    /// <summary>Bloqueios do profissional que tocam a data.</summary>
    Task<IReadOnlyList<Team.ScheduleBlock>> GetBlocksAsync(
        Guid tenantId,
        Guid professionalId,
        DateOnly date,
        CancellationToken cancellationToken = default);
}
