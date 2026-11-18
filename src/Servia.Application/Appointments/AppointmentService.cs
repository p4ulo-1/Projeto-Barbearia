using Servia.Domain.Appointments;
using Servia.Domain.Common;

namespace Servia.Application.Appointments;

/// <summary>
/// Casos de uso de agendamento.
///
/// <b>Um único dono da validação.</b> Criação e remarcação passam por
/// <see cref="AvailabilityService.EvaluateAsync"/>, que é a mesma regra usada
/// pela consulta de horários. O controller não valida nada: ele traduz o
/// resultado em HTTP. Sem isso, cada canal teria a sua própria cópia da regra
/// e elas divergiriam.
///
/// <b>A duração vem do serviço.</b> O comando não carrega duração: quem
/// agenda não decide quanto tempo o atendimento ocupa. A duração é lida do
/// serviço persistido dentro da transação.
///
/// <b>A regra de não sobreposição (R5)</b> depende do conjunto de agendamentos
/// do profissional — não pode ser protegida por uma única entidade sem que ela
/// passe a depender de dados externos (ver docs/av2/analise-do-modelo.md,
/// Decisão 1). Por isso vive aqui, junto com a verificação e a gravação na
/// mesma transação serializável.
///
/// A comparação de intervalos usa <see cref="TimeRange"/>, que trabalha em
/// minutos desde a meia-noite e portanto não quebra quando o atendimento
/// cruza a meia-noite (DEF-001).
/// </summary>
public sealed class AppointmentService(
    IAppointmentRepository repository,
    AvailabilityService availability,
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
    ///
    /// A validação de serviço, profissional, vínculo, jornada, bloqueio e
    /// conflito acontece <b>dentro</b> da transação serializável: verificar
    /// fora dela permitiria que outra requisição ocupasse o horário entre a
    /// verificação e a gravação.
    /// </summary>
    public async Task<AppointmentResult> CreateAsync(
        CreateAppointmentCommand command,
        Servia.Domain.Customers.Customer? newCustomer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        try
        {
            return await repository.ExecuteSerializableAsync(
                async token =>
                {
                    var evaluation = await availability.EvaluateAsync(
                        command.TenantId,
                        command.ProfessionalId,
                        command.ServiceId,
                        command.Date,
                        command.Time,
                        token);

                    if (!evaluation.Availability.IsAvailable)
                    {
                        return evaluation.Availability.IsConflict
                            ? AppointmentResult.Conflict(evaluation.Availability.Reason!)
                            : AppointmentResult.Invalid(evaluation.Availability.Reason!);
                    }

                    // A duracao vem do servico persistido, nunca do pedido.
                    var durationMinutes = await ResolveDurationAsync(
                        command.TenantId, command.ServiceId, token);

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
                        durationMinutes,
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
    /// Remarca um agendamento, aplicando as mesmas regras da criação.
    ///
    /// <b>O próprio agendamento é excluído da verificação de sobreposição.</b>
    /// Sem isso, remarcar para o mesmo horário (ou para um horário que apenas
    /// encosta no intervalo atual) seria rejeitado como conflito consigo mesmo.
    ///
    /// <b>Uma tentativa inválida não altera o registro.</b> A entidade só é
    /// mutada depois que a avaliação aprova o novo horário, e a gravação
    /// acontece na mesma transação serializável.
    /// </summary>
    public async Task<AppointmentResult> RescheduleAsync(
        Guid tenantId,
        Guid appointmentId,
        DateOnly date,
        TimeOnly time,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            return AppointmentResult.Invalid("Estabelecimento não identificado.");
        }

        if (date == default)
        {
            return AppointmentResult.Invalid("A nova data do agendamento é obrigatória.");
        }

        try
        {
            return await repository.ExecuteSerializableAsync(
                async token =>
                {
                    var appointment = await repository.GetByIdAsync(tenantId, appointmentId, token);

                    if (appointment is null)
                    {
                        return AppointmentResult.NotFound("Agendamento não encontrado.");
                    }

                    if (!appointment.IsConfirmed)
                    {
                        return AppointmentResult.Invalid(
                            "Somente agendamentos confirmados podem ser remarcados.");
                    }

                    var evaluation = await availability.EvaluateAsync(
                        tenantId,
                        appointment.ProfessionalId,
                        appointment.ServiceId,
                        date,
                        time,
                        token,
                        excludeAppointmentId: appointment.Id,
                        // A remarcação valida o intervalo com a duração
                        // registrada no agendamento, não com a atual do
                        // serviço: o intervalo validado precisa ser o mesmo
                        // que o agendamento ocupa.
                        durationMinutesOverride: appointment.DurationMinutes);

                    if (!evaluation.Availability.IsAvailable)
                    {
                        return evaluation.Availability.IsConflict
                            ? AppointmentResult.Conflict(evaluation.Availability.Reason!)
                            : AppointmentResult.Invalid(evaluation.Availability.Reason!);
                    }

                    appointment.Reschedule(date, time);

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
            return AppointmentResult.Conflict("Este horário conflita com outro agendamento.");
        }
    }

    /// <summary>
    /// Duração do serviço persistido. A avaliação de disponibilidade já
    /// confirmou que o serviço existe e está ativo; a leitura aqui é a fonte
    /// da duração gravada no agendamento.
    /// </summary>
    private async Task<int> ResolveDurationAsync(
        Guid tenantId,
        Guid serviceId,
        CancellationToken cancellationToken)
    {
        var service = await repository.FindServiceAsync(tenantId, serviceId, cancellationToken);

        if (service is null || !service.IsActive)
        {
            throw new DomainRuleException("O serviço não está disponível para agendamento.");
        }

        return service.DurationMinutes;
    }

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