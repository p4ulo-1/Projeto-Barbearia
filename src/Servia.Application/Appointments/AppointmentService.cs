using Servia.Domain.Appointments;
using Servia.Domain.Common;

namespace Servia.Application.Appointments;

/// <summary>
/// Casos de uso de agendamento.
///
/// Orquestra o domínio e o repositório. A regra de não sobreposição (R5) vive
/// aqui porque depende do conjunto de agendamentos do profissional — não pode
/// ser protegida por uma única entidade sem que ela passe a depender de dados
/// externos (ver docs/av2/analise-do-modelo.md, Decisão 1).
///
/// A comparação de intervalos usa <see cref="TimeRange"/>, que trabalha em
/// minutos desde a meia-noite e portanto não quebra quando o atendimento
/// cruza a meia-noite (DEF-001).
/// </summary>
public sealed class AppointmentService(
    IAppointmentRepository repository,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Cria um agendamento, rejeitando conflito de horário.
    /// </summary>
    public Task<AppointmentResult> CreateAsync(
        CreateAppointmentCommand command,
        CancellationToken cancellationToken = default) =>
        CreateAsync(command, newCustomer: null, cancellationToken);

    /// <summary>
    /// Cria um agendamento, rejeitando conflito de horário.
    ///
    /// <paramref name="newCustomer"/> permite criar o cliente na mesma
    /// transação do agendamento (C6). Quando informado, o cliente só é
    /// persistido se o agendamento for criado — um horário ocupado não deixa
    /// cliente órfão.
    /// </summary>
    public async Task<AppointmentResult> CreateAsync(
        CreateAppointmentCommand command,
        Servia.Domain.Customers.Customer? newCustomer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.DurationMinutes <= 0)
        {
            return AppointmentResult.Invalid("A duração do atendimento deve ser maior que zero.");
        }

        var occupied = TimeRange.From(command.Time, command.DurationMinutes);

        // Verificacao e gravacao na mesma transacao serializavel (Etapa 5).
        // Sem isso, duas requisicoes simultaneas consultam, ambas veem o
        // horario livre e ambas gravam.
        try
        {
            return await repository.ExecuteSerializableAsync(
                async token =>
                {
                    var existing = await repository.GetActiveByProfessionalAndDateAsync(
                        command.TenantId,
                        command.ProfessionalId,
                        command.Date,
                        token);

                    if (HasOverlap(existing, occupied))
                    {
                        return AppointmentResult.Conflict(
                            "Este horário conflita com outro agendamento.");
                    }

                    // C6: cliente novo entra na mesma transacao do agendamento.
                    if (newCustomer is not null)
                    {
                        await repository.AddCustomerAsync(newCustomer, token);
                    }

                    var appointment = Appointment.Create(
                        command.TenantId,
                        command.CustomerId,
                        command.ServiceId,
                        command.ProfessionalId,
                        command.Date,
                        command.Time,
                        command.DurationMinutes,
                        timeProvider.GetUtcNow());

                    await repository.AddAsync(appointment, token);
                    await repository.SaveChangesAsync(token);

                    return AppointmentResult.Success(appointment);
                },
                cancellationToken);
        }
        catch (DomainRuleException exception)
        {
            return AppointmentResult.Invalid(exception.Message);
        }
        catch (Exception exception) when (IsConcurrencyConflict(exception))
        {
            // Violacao de constraint ou aborto por serializacao: outra
            // transacao gravou primeiro.
            return AppointmentResult.Conflict("Este horário conflita com outro agendamento.");
        }
    }

    /// <summary>
    /// Indica se o intervalo pretendido se sobrepõe a algum agendamento ativo.
    ///
    /// Regra R5: dois agendamentos do mesmo profissional não podem ocupar
    /// intervalos sobrepostos. Agendamentos cancelados não bloqueiam o horário.
    /// </summary>
    /// <summary>
    /// Indica se a excecao representa conflito de concorrencia.
    ///
    /// A camada de aplicacao nao referencia EF Core: a deteccao e por
    /// mensagem, que e o contrato estavel do provider.
    /// </summary>
    private static bool IsConcurrencyConflict(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            var message = current.Message;

            if (message.Contains("23505", StringComparison.Ordinal)
                || message.Contains("40001", StringComparison.Ordinal)
                || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
                || message.Contains("could not serialize", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool HasOverlap(
        IEnumerable<Appointment> existing,
        TimeRange candidate)
    {
        ArgumentNullException.ThrowIfNull(existing);

        return existing.Any(appointment =>
            appointment.IsConfirmed && appointment.OccupiedRange.Overlaps(candidate));
    }
}
