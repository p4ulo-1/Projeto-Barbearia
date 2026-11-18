using Servia.Domain.Appointments;
using Servia.Domain.Common;
using Servia.Domain.Team;

namespace Servia.Application.Appointments;

/// <summary>
/// Cálculo de disponibilidade centralizado (C9).
///
/// <b>Dois conceitos independentes:</b>
///
/// <b>A. Expediente do estabelecimento</b> — o horário em que o
/// estabelecimento atende ao público, configurado pelo administrador
/// (por dia da semana, com dias fechados). Serve para informar se o
/// estabelecimento está aberto; <b>não</b> determina nem restringe
/// agendamentos.
///
/// <b>B. Disponibilidade do profissional</b> — vínculo com o serviço,
/// jornada, bloqueios e agendamentos confirmados, considerando a duração
/// integral do serviço.
///
/// <b>Agendamentos dependem exclusivamente do profissional (B).</b> Um
/// profissional pode ter horário livre para agendamento mesmo com o
/// estabelecimento fechado, e o estabelecimento pode estar aberto mesmo sem
/// nenhum profissional disponível. As duas disponibilidades nunca se
/// restringem mutuamente.
///
/// <b>Um único caminho de validação.</b> A consulta de horários, a criação e
/// a remarcação passam por <see cref="EvaluateAsync"/>. Nenhum canal —
/// interno ou público — reimplementa a regra. Sem isso, a agenda ofereceria
/// um horário que a gravação rejeita, ou aceitaria um que a consulta esconde.
///
/// <b>Serviço é parte da regra.</b> Um profissional só está disponível para
/// um serviço que executa. A duração vem do serviço persistido, nunca do
/// pedido do cliente.
/// </summary>
public sealed class AvailabilityService(IAvailabilityRepository repository)
{
    /// <summary>
    /// Indica se o estabelecimento está aberto ao público na data, segundo o
    /// expediente configurado. É informativo — não restringe agendamentos.
    /// </summary>
    public async Task<bool> IsEstablishmentOpenAsync(
        Guid tenantId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var tenant = await repository.FindTenantAsync(tenantId, cancellationToken);

        return tenant is not null && tenant.Schedule.IsOpenOn(date);
    }

    /// <summary>
    /// Horários livres de um profissional na data, para um serviço do tenant.
    ///
    /// Depende apenas do profissional e do serviço — vínculo, jornada,
    /// bloqueios e agendamentos. O expediente do estabelecimento não é
    /// consultado. Serviço inexistente, inativo ou não executado pelo
    /// profissional resulta em lista vazia: não há horário a oferecer.
    /// </summary>
    public async Task<IReadOnlyList<TimeOnly>> GetAvailableSlotsAsync(
        Guid tenantId,
        Guid professionalId,
        Guid serviceId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var evaluation = await EvaluateAsync(
            tenantId, professionalId, serviceId, date, time: null, cancellationToken);

        return evaluation.Slots;
    }

    /// <summary>
    /// Indica se um horário específico pode ser agendado (C9 — validação).
    ///
    /// Usa exatamente as mesmas regras da consulta. O expediente do
    /// estabelecimento não participa.
    /// </summary>
    public async Task<SlotAvailability> CheckSlotAsync(
        Guid tenantId,
        Guid professionalId,
        Guid serviceId,
        DateOnly date,
        TimeOnly time,
        CancellationToken cancellationToken = default)
    {
        var evaluation = await EvaluateAsync(
            tenantId, professionalId, serviceId, date, time, cancellationToken);

        return evaluation.Availability;
    }

    /// <summary>
    /// Regra única de disponibilidade, compartilhada por consulta, criação e
    /// remarcação.
    ///
    /// Quando <paramref name="time"/> é nulo, avalia o dia inteiro e devolve
    /// os horários livres. Quando informado, avalia aquele horário e devolve
    /// o veredito em <see cref="SlotEvaluation.Availability"/>.
    ///
    /// <paramref name="excludeAppointmentId"/> permite que a remarcação
    /// ignore o próprio agendamento ao verificar sobreposição — sem isso, o
    /// agendamento conflitaria consigo mesmo.
    ///
    /// <paramref name="durationMinutesOverride"/> permite que a remarcação
    /// valide o intervalo com a duração <b>registrada no agendamento</b>, e
    /// não com a duração atual do serviço. A duração de um agendamento é
    /// fixada na criação e preservada no ciclo de vida: se o serviço mudar de
    /// 30 para 45 minutos, o agendamento antigo continua ocupando 30. Sem
    /// isso, o intervalo validado divergiria do intervalo efetivamente
    /// ocupado. Quando nulo, usa a duração atual do serviço — o caso da
    /// consulta e da criação.
    /// </summary>
    public async Task<SlotEvaluation> EvaluateAsync(
        Guid tenantId,
        Guid professionalId,
        Guid serviceId,
        DateOnly date,
        TimeOnly? time,
        CancellationToken cancellationToken = default,
        Guid? excludeAppointmentId = null,
        int? durationMinutesOverride = null)
    {
        var service = await repository.FindServiceAsync(tenantId, serviceId, cancellationToken);

        if (!BookingEligibility.IsServiceBookable(service))
        {
            return SlotEvaluation.Empty(
                "O serviço não está disponível para agendamento.");
        }

        var professional = await repository.FindProfessionalAsync(
            tenantId, professionalId, cancellationToken);

        if (professional is null || !BookingEligibility.IsOperationallyEligible(professional))
        {
            return SlotEvaluation.Empty("O profissional não está disponível.");
        }

        var isLinked = await repository.IsServiceLinkedAsync(
            tenantId, professionalId, serviceId, cancellationToken);

        if (!BookingEligibility.CanExecute(professional, service, isLinked))
        {
            return SlotEvaluation.Empty(
                "O profissional não executa o serviço solicitado.");
        }

        if (!professional.Schedule.WorksOn(date))
        {
            return SlotEvaluation.Empty("O profissional não atende nesta data.");
        }

        // A duração efetiva é a do agendamento, quando informada (remarcação);
        // caso contrário, a atual do serviço (consulta e criação).
        var durationMinutes = durationMinutesOverride ?? service.DurationMinutes;

        var occupied = await repository.GetConfirmedAppointmentsAsync(
            tenantId, professionalId, date, cancellationToken);

        // A remarcação não pode conflitar com o próprio agendamento.
        if (excludeAppointmentId.HasValue)
        {
            occupied = occupied
                .Where(appointment => appointment.Id != excludeAppointmentId.Value)
                .ToList();
        }

        var blocks = await repository.GetBlocksAsync(
            tenantId, professionalId, date, cancellationToken);

        if (time.HasValue)
        {
            return SlotEvaluation.ForSlot(
                EvaluateSlot(professional, date, time.Value, durationMinutes, occupied, blocks));
        }

        var slots = new List<TimeOnly>();

        foreach (var slot in professional.Schedule.GenerateSlots(durationMinutes))
        {
            if (IsSlotFree(professional, date, slot, durationMinutes, occupied, blocks))
            {
                slots.Add(slot);
            }
        }

        return SlotEvaluation.WithSlots(slots);
    }

    /// <summary>
    /// Veredito de um horário específico. Conflito com agendamento existente
    /// é 409 (disputa de horário); bloqueio e fora de jornada são 400.
    /// </summary>
    private static SlotAvailability EvaluateSlot(
        Professional professional,
        DateOnly date,
        TimeOnly time,
        int durationMinutes,
        IReadOnlyList<Appointment> occupied,
        IReadOnlyList<ScheduleBlock> blocks)
    {
        if (!professional.Schedule.FitsWithinSchedule(time, durationMinutes))
        {
            return SlotAvailability.Unavailable(
                "O horário está fora do expediente do profissional para este serviço.");
        }

        var candidate = TimeRange.From(time, durationMinutes);

        if (occupied.Any(appointment => appointment.OccupiedRange.Overlaps(candidate)))
        {
            return SlotAvailability.Conflict("Este horário conflita com outro agendamento.");
        }

        var slotStart = new DateTimeOffset(date.ToDateTime(time), TimeSpan.Zero);
        var slotEnd = slotStart.AddMinutes(durationMinutes);

        if (blocks.Any(block => block.Overlaps(slotStart, slotEnd)))
        {
            return SlotAvailability.Unavailable(
                "O profissional não tem disponibilidade neste horário.");
        }

        return SlotAvailability.Available();
    }

    /// <summary>
    /// Regra única de "horário livre" do profissional: dentro da jornada, sem
    /// conflito de agendamento confirmado e sem bloqueio, considerando a
    /// duração inteira. Não consulta o expediente do estabelecimento.
    /// </summary>
    private static bool IsSlotFree(
        Professional professional,
        DateOnly date,
        TimeOnly time,
        int durationMinutes,
        IReadOnlyList<Appointment> occupied,
        IReadOnlyList<ScheduleBlock> blocks) =>
        EvaluateSlot(professional, date, time, durationMinutes, occupied, blocks).IsAvailable;
}

/// <summary>
/// Resultado de uma avaliação de disponibilidade.
///
/// Carrega os horários livres (consulta do dia) e o veredito de um horário
/// específico (validação). Os dois vêm da mesma execução da regra, o que
/// garante que consulta e gravação nunca divirjam.
/// </summary>
public sealed record SlotEvaluation(
    IReadOnlyList<TimeOnly> Slots,
    SlotAvailability Availability)
{
    public static SlotEvaluation WithSlots(IReadOnlyList<TimeOnly> slots) =>
        new(slots, SlotAvailability.Available());

    public static SlotEvaluation ForSlot(SlotAvailability availability) =>
        new([], availability);

    /// <summary>Serviço ou profissional inelegível: sem horários e com motivo.</summary>
    public static SlotEvaluation Empty(string reason) =>
        new([], SlotAvailability.Unavailable(reason));
}

/// <summary>Resultado da verificação de um horário específico (C9).</summary>
public sealed record SlotAvailability(bool IsAvailable, bool IsConflict, string? Reason)
{
    public static SlotAvailability Available() => new(true, false, null);

    /// <summary>Horário inválido (fora de expediente, bloqueio, dia sem trabalho).</summary>
    public static SlotAvailability Unavailable(string reason) => new(false, false, reason);

    /// <summary>Horário válido, mas já ocupado por outro agendamento (409).</summary>
    public static SlotAvailability Conflict(string reason) => new(false, true, reason);
}
