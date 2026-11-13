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
/// <b>B. Disponibilidade do profissional</b> — jornada, bloqueios e
/// agendamentos confirmados, considerando a duração integral do serviço.
///
/// <b>Agendamentos dependem exclusivamente do profissional (B).</b> Um
/// profissional pode ter horário livre para agendamento mesmo com o
/// estabelecimento fechado, e o estabelecimento pode estar aberto mesmo sem
/// nenhum profissional disponível. As duas disponibilidades nunca se
/// restringem mutuamente.
///
/// Tanto a consulta quanto a validação no momento do agendamento usam as
/// mesmas regras do profissional, garantindo consulta e gravação idênticas.
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
    /// Horários livres de um profissional na data, para um serviço de
    /// <paramref name="durationMinutes"/> (C9-B).
    ///
    /// Depende apenas do profissional — jornada, bloqueios e agendamentos.
    /// O expediente do estabelecimento não é consultado.
    /// </summary>
    public async Task<IReadOnlyList<TimeOnly>> GetAvailableSlotsAsync(
        Guid tenantId,
        Guid professionalId,
        DateOnly date,
        int durationMinutes,
        CancellationToken cancellationToken = default)
    {
        if (durationMinutes <= 0)
        {
            return [];
        }

        var professional = await repository.FindProfessionalAsync(tenantId, professionalId, cancellationToken);

        if (professional is null || !professional.IsActive || !professional.Schedule.WorksOn(date))
        {
            return [];
        }

        var occupied = await repository.GetConfirmedAppointmentsAsync(
            tenantId, professionalId, date, cancellationToken);

        var blocks = await repository.GetBlocksAsync(tenantId, professionalId, date, cancellationToken);

        var slots = new List<TimeOnly>();

        foreach (var slot in professional.Schedule.GenerateSlots(durationMinutes))
        {
            if (IsSlotFree(professional, date, slot, durationMinutes, occupied, blocks))
            {
                slots.Add(slot);
            }
        }

        return slots;
    }

    /// <summary>
    /// Indica se um horário específico pode ser agendado (C9 — validação).
    ///
    /// Usa exatamente as mesmas regras da consulta, baseadas apenas no
    /// profissional. O expediente do estabelecimento não participa.
    /// </summary>
    public async Task<SlotAvailability> CheckSlotAsync(
        Guid tenantId,
        Guid professionalId,
        DateOnly date,
        TimeOnly time,
        int durationMinutes,
        CancellationToken cancellationToken = default)
    {
        if (durationMinutes <= 0)
        {
            return SlotAvailability.Unavailable("A duração do serviço deve ser maior que zero.");
        }

        var professional = await repository.FindProfessionalAsync(tenantId, professionalId, cancellationToken);

        if (professional is null || !professional.IsActive)
        {
            return SlotAvailability.Unavailable("O profissional não está disponível.");
        }

        if (!professional.Schedule.WorksOn(date))
        {
            return SlotAvailability.Unavailable("O profissional não atende nesta data.");
        }

        if (!professional.Schedule.FitsWithinSchedule(time, durationMinutes))
        {
            return SlotAvailability.Unavailable(
                "O horário está fora do expediente do profissional para este serviço.");
        }

        var occupied = await repository.GetConfirmedAppointmentsAsync(
            tenantId, professionalId, date, cancellationToken);

        var blocks = await repository.GetBlocksAsync(tenantId, professionalId, date, cancellationToken);

        // Conflito com agendamento existente e um 409 (disputa de horario),
        // nao um 400 (pedido invalido). Bloqueio e fora de expediente sao 400.
        var candidate = TimeRange.From(time, durationMinutes);

        if (occupied.Any(appointment => appointment.OccupiedRange.Overlaps(candidate)))
        {
            return SlotAvailability.Conflict("Este horário conflita com outro agendamento.");
        }

        var slotStart = new DateTimeOffset(date.ToDateTime(time), TimeSpan.Zero);
        var slotEnd = slotStart.AddMinutes(durationMinutes);

        if (blocks.Any(block => block.Overlaps(slotStart, slotEnd)))
        {
            return SlotAvailability.Unavailable("O profissional não tem disponibilidade neste horário.");
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
        IReadOnlyList<ScheduleBlock> blocks)
    {
        if (!professional.Schedule.FitsWithinSchedule(time, durationMinutes))
        {
            return false;
        }

        var candidate = TimeRange.From(time, durationMinutes);

        if (occupied.Any(appointment => appointment.OccupiedRange.Overlaps(candidate)))
        {
            return false;
        }

        var slotStart = new DateTimeOffset(date.ToDateTime(time), TimeSpan.Zero);
        var slotEnd = slotStart.AddMinutes(durationMinutes);

        return !blocks.Any(block => block.Overlaps(slotStart, slotEnd));
    }
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
