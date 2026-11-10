using Servia.Domain.Appointments;
using Servia.Domain.Common;

namespace Servia.Tests.Appointments;

/// <summary>
/// Regras R3 (jornada válida) e R4 (serviço cabe na jornada).
/// Cobre os critérios F1-CA2, F1-CA3, R3-CA1..3 e R4-CA1..3.
/// </summary>
public class WorkScheduleTests
{
    private static readonly DayOfWeek[] Weekdays =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    private static WorkSchedule DefaultSchedule() =>
        WorkSchedule.Create(Weekdays, new TimeOnly(9, 0), new TimeOnly(18, 0));

    private static DateOnly Monday() => new(2026, 11, 9);

    private static DateOnly Sunday() => new(2026, 11, 8);

    // ---------- R3: jornada válida ----------

    [Fact]
    public void Criar_Jornada_Valida_Aceita()
    {
        var schedule = DefaultSchedule();

        Assert.Equal(new TimeOnly(9, 0), schedule.StartTime);
        Assert.Equal(new TimeOnly(18, 0), schedule.EndTime);
        Assert.Equal(5, schedule.WorkDays.Count);
    }

    [Fact]
    public void Criar_Jornada_Inicio_Igual_Fim_Lanca_Excecao()
    {
        var exception = Assert.Throws<DomainRuleException>(() =>
            WorkSchedule.Create(Weekdays, new TimeOnly(9, 0), new TimeOnly(9, 0)));

        Assert.Contains("anterior ao fim", exception.Message);
    }

    [Fact]
    public void Criar_Jornada_Inicio_Maior_Fim_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            WorkSchedule.Create(Weekdays, new TimeOnly(18, 0), new TimeOnly(9, 0)));
    }

    [Fact]
    public void Criar_Jornada_Sem_Dias_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            WorkSchedule.Create([], new TimeOnly(9, 0), new TimeOnly(18, 0)));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(121)]
    public void Criar_Jornada_Granularidade_Invalida_Lanca_Excecao(int interval)
    {
        Assert.Throws<DomainRuleException>(() =>
            WorkSchedule.Create(Weekdays, new TimeOnly(9, 0), new TimeOnly(18, 0), interval));
    }

    // ---------- F1-CA2: dia de trabalho ----------

    [Fact]
    public void WorksOn_Retorna_True_Em_Dia_Trabalhado()
    {
        Assert.True(DefaultSchedule().WorksOn(Monday()));
    }

    [Fact]
    public void WorksOn_Retorna_Falso_Em_Dia_Nao_Trabalhado()
    {
        Assert.False(DefaultSchedule().WorksOn(Sunday()));
    }

    // ---------- R4: serviço cabe na jornada ----------

    [Fact]
    public void FitsWithinSchedule_Aceita_Horario_Que_Termina_No_Fim()
    {
        // 17:00 + 60 min = 18:00, exatamente no fim.
        Assert.True(DefaultSchedule().FitsWithinSchedule(new TimeOnly(17, 0), 60));
    }

    [Fact]
    public void FitsWithinSchedule_Rejeita_Horario_Que_Ultrapassa_Fim()
    {
        // 17:30 + 60 min = 18:30, ultrapassa.
        Assert.False(DefaultSchedule().FitsWithinSchedule(new TimeOnly(17, 30), 60));
    }

    [Fact]
    public void FitsWithinSchedule_Rejeita_Horario_Antes_Da_Abertura()
    {
        Assert.False(DefaultSchedule().FitsWithinSchedule(new TimeOnly(8, 30), 30));
    }

    [Fact]
    public void FitsWithinSchedule_Rejeita_Duracao_Invalida()
    {
        Assert.False(DefaultSchedule().FitsWithinSchedule(new TimeOnly(10, 0), 0));
    }

    // ---------- Geração de horários ----------

    [Fact]
    public void GenerateSlots_Respeita_Granularidade_E_Duracao()
    {
        var slots = DefaultSchedule().GenerateSlots(60).ToArray();

        Assert.Equal(new TimeOnly(9, 0), slots.First());
        Assert.Equal(new TimeOnly(17, 0), slots.Last());
        Assert.Equal(17, slots.Length);
    }
}
