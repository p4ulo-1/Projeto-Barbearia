namespace Servia.Domain.Appointments;

/// <summary>
/// Contrato de persistência de agendamentos.
///
/// Definido no domínio para que a camada de aplicação dependa apenas da
/// abstração, nunca da implementação (ADR-004). A implementação concreta
/// vive em Infrastructure.
///
/// Todas as consultas são escopadas por tenant: o repositório nunca expõe
/// um método que atravesse a fronteira de isolamento (ADR-016).
/// </summary>
public interface IAppointmentRepository
{
    /// <summary>
    /// Retorna os agendamentos ativos de um profissional em uma data.
    /// Agendamentos cancelados são excluídos, pois não bloqueiam o horário.
    /// </summary>
    Task<IReadOnlyList<Appointment>> GetActiveByProfessionalAndDateAsync(
        Guid tenantId,
        Guid professionalId,
        DateOnly date,
        CancellationToken cancellationToken = default);

    /// <summary>Busca um agendamento pelo identificador dentro do tenant.</summary>
    Task<Appointment?> GetByIdAsync(
        Guid tenantId,
        Guid appointmentId,
        CancellationToken cancellationToken = default);

    /// <summary>Adiciona um novo agendamento.</summary>
    Task AddAsync(Appointment appointment, CancellationToken cancellationToken = default);

    /// <summary>Persiste as alterações pendentes.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executa a operação dentro de uma transação com nível de isolamento
    /// serializável (Etapa 5 da auditoria).
    ///
    /// A verificação de disponibilidade e a gravação precisam ser atômicas:
    /// sem isso, duas requisições simultâneas consultam, ambas veem o
    /// horário livre e ambas gravam.
    ///
    /// O nível serializável faz o PostgreSQL abortar uma das transações
    /// concorrentes com erro de serialização, que o chamador traduz em
    /// conflito de negócio.
    /// </summary>
    Task<T> ExecuteSerializableAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}
