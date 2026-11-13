using Servia.Domain.Common;

namespace Servia.Domain.Tenancy;

/// <summary>
/// Expediente semanal do estabelecimento.
///
/// <b>Independente dos profissionais:</b> este expediente descreve quando o
/// estabelecimento atende ao público; ele <b>não</b> determina nem restringe
/// os agendamentos de serviços. O agendamento depende exclusivamente da
/// disponibilidade individual de cada profissional.
///
/// <b>Por dia da semana:</b> cada dia tem seu próprio horário e pode estar
/// fechado. Não há um horário único para toda a semana.
///
/// <b>Invariantes:</b> há exatamente sete dias; um dia aberto tem abertura
/// anterior ao fechamento; um dia fechado não precisa de horário.
/// </summary>
public sealed class EstablishmentSchedule
{
    private readonly List<EstablishmentDaySchedule> _days = [];

    /// <summary>
    /// Expediente padrão: <b>fechado</b> todos os dias, usado enquanto o
    /// administrador não configura um expediente explícito.
    ///
    /// Fechado por padrão é a leitura conservadora: o estabelecimento só
    /// aparece aberto ao público quando o administrador o configura. Isso
    /// <b>não</b> restringe agendamentos — estes dependem só do profissional.
    /// </summary>
    public static EstablishmentSchedule ClosedByDefault() => CreateDefault(open: false);

    private static EstablishmentSchedule CreateDefault(bool open)
    {
        var schedule = new EstablishmentSchedule();

        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            schedule._days.Add(new EstablishmentDaySchedule(day, open, open ? TimeOnly.MinValue : null, open ? new TimeOnly(23, 59) : null));
        }

        return schedule;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core. Não valida invariantes:
    /// a validação ocorre em <see cref="Create"/>.
    /// </summary>
    private EstablishmentSchedule()
    {
    }

    public IReadOnlyCollection<EstablishmentDaySchedule> Days => _days;

    /// <summary>
    /// Cria o expediente a partir das configurações de cada dia. Exige os
    /// sete dias da semana.
    /// </summary>
    public static EstablishmentSchedule Create(IEnumerable<EstablishmentDaySchedule> days)
    {
        ArgumentNullException.ThrowIfNull(days);

        var materialized = days.ToList();

        if (materialized.Count != 7)
        {
            throw new DomainRuleException("O expediente deve configurar os sete dias da semana.");
        }

        if (materialized.Select(day => day.DayOfWeek).Distinct().Count() != 7)
        {
            throw new DomainRuleException("O expediente não pode repetir dias da semana.");
        }

        foreach (var day in materialized)
        {
            day.EnsureValid();
        }

        var schedule = new EstablishmentSchedule();
        schedule._days.AddRange(materialized);

        return schedule;
    }

    /// <summary>Indica se o estabelecimento está aberto na data.</summary>
    public bool IsOpenOn(DateOnly date)
    {
        var day = _days.FirstOrDefault(item => item.DayOfWeek == date.DayOfWeek);

        return day is not null && day.IsOpen;
    }

    /// <summary>Horário de funcionamento do dia, ou nulo se fechado.</summary>
    public TimeRange? HoursOn(DateOnly date)
    {
        var day = _days.FirstOrDefault(item => item.DayOfWeek == date.DayOfWeek);

        return day is null || !day.IsOpen ? null : day.Hours;
    }
}

/// <summary>Expediente de um dia da semana.</summary>
public sealed class EstablishmentDaySchedule
{
    internal EstablishmentDaySchedule(DayOfWeek dayOfWeek, bool isOpen, TimeOnly? openTime, TimeOnly? closeTime)
    {
        DayOfWeek = dayOfWeek;
        IsOpen = isOpen;
        OpenTime = openTime;
        CloseTime = closeTime;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core. Não valida invariantes:
    /// a validação ocorre em <see cref="Create"/>.
    /// </summary>
    private EstablishmentDaySchedule()
    {
    }

    public DayOfWeek DayOfWeek { get; private set; }

    public bool IsOpen { get; private set; }

    public TimeOnly? OpenTime { get; private set; }

    public TimeOnly? CloseTime { get; private set; }

    /// <summary>Horário de funcionamento como intervalo comparável.</summary>
    public TimeRange Hours => TimeRange.Between(OpenTime!.Value, CloseTime!.Value);

    public static EstablishmentDaySchedule Open(DayOfWeek dayOfWeek, TimeOnly openTime, TimeOnly closeTime)
    {
        var day = new EstablishmentDaySchedule(dayOfWeek, true, openTime, closeTime);
        day.EnsureValid();

        return day;
    }

    public static EstablishmentDaySchedule Closed(DayOfWeek dayOfWeek) =>
        new(dayOfWeek, false, null, null);

    /// <summary>Valida as invariantes do dia: um dia aberto precisa de horário válido.</summary>
    public void EnsureValid()
    {
        if (!Enum.IsDefined(DayOfWeek))
        {
            throw new DomainRuleException("O dia da semana é inválido.");
        }

        if (!IsOpen)
        {
            return;
        }

        if (OpenTime is null || CloseTime is null)
        {
            throw new DomainRuleException(
                $"O dia {DayOfWeek} está aberto mas não tem horário de funcionamento.");
        }

        if (OpenTime >= CloseTime)
        {
            throw new DomainRuleException(
                $"O horário de abertura de {DayOfWeek} deve ser anterior ao fechamento.");
        }
    }
}
