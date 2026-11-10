namespace Servia.Domain.Common;

/// <summary>
/// Objeto de valor que representa um intervalo de tempo dentro de um dia.
///
/// Os limites são armazenados em MINUTOS DESDE A MEIA-NOITE, e não como
/// <see cref="TimeOnly"/>. Motivo: <c>TimeOnly</c> é cíclico — somar 30 minutos
/// a 23:50 resulta em 00:20, o que quebra qualquer comparação de intervalos
/// que cruze a meia-noite. Este tipo elimina essa classe de defeito na origem
/// (ver DEF-001 em docs/av2/registro-de-depuracao.md).
/// </summary>
public readonly record struct TimeRange
{
    public const int MinutesPerDay = 24 * 60;

    private TimeRange(int startMinutes, int endMinutes)
    {
        StartMinutes = startMinutes;
        EndMinutes = endMinutes;
    }

    /// <summary>Minutos desde a meia-noite em que o intervalo começa.</summary>
    public int StartMinutes { get; }

    /// <summary>Minutos desde a meia-noite em que o intervalo termina.</summary>
    public int EndMinutes { get; }

    public int DurationMinutes => EndMinutes - StartMinutes;

    public TimeOnly Start => TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(StartMinutes));

    public TimeOnly End => TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(EndMinutes));

    /// <summary>
    /// Cria um intervalo a partir de um horário de início e uma duração.
    /// </summary>
    public static TimeRange From(TimeOnly start, int durationMinutes)
    {
        if (durationMinutes <= 0)
        {
            throw new DomainRuleException("A duração deve ser maior que zero.");
        }

        var startMinutes = ToMinutes(start);
        return new TimeRange(startMinutes, startMinutes + durationMinutes);
    }

    /// <summary>
    /// Cria um intervalo entre dois horários do mesmo dia.
    /// </summary>
    public static TimeRange Between(TimeOnly start, TimeOnly end)
    {
        var startMinutes = ToMinutes(start);
        var endMinutes = ToMinutes(end);

        if (startMinutes >= endMinutes)
        {
            throw new DomainRuleException("O início do intervalo deve ser anterior ao fim.");
        }

        return new TimeRange(startMinutes, endMinutes);
    }

    /// <summary>
    /// Indica se este intervalo se sobrepõe a outro.
    /// A comparação é feita em minutos absolutos, portanto funciona
    /// corretamente mesmo quando o intervalo cruza a meia-noite.
    /// </summary>
    public bool Overlaps(TimeRange other) =>
        StartMinutes < other.EndMinutes && EndMinutes > other.StartMinutes;

    /// <summary>
    /// Indica se este intervalo está inteiramente contido em outro.
    /// </summary>
    public bool IsContainedIn(TimeRange container) =>
        StartMinutes >= container.StartMinutes && EndMinutes <= container.EndMinutes;

    public static int ToMinutes(TimeOnly time) => time.Hour * 60 + time.Minute;

    public override string ToString() => $"{Start:HH\\:mm}-{End:HH\\:mm}";
}
