using Servia.Domain.Common;

namespace Servia.Tests.Common;

/// <summary>
/// Verifica o objeto de valor TimeRange, incluindo o cenário do DEF-001:
/// intervalos que cruzam a meia-noite devem ser comparados corretamente.
/// </summary>
public class TimeRangeTests
{
    [Fact]
    public void From_Calcula_Duracao_Corretamente()
    {
        var range = TimeRange.From(new TimeOnly(10, 0), 90);

        Assert.Equal(600, range.StartMinutes);
        Assert.Equal(690, range.EndMinutes);
        Assert.Equal(90, range.DurationMinutes);
    }

    [Fact]
    public void From_Rejeita_Duracao_Invalida()
    {
        Assert.Throws<DomainRuleException>(() => TimeRange.From(new TimeOnly(10, 0), 0));
        Assert.Throws<DomainRuleException>(() => TimeRange.From(new TimeOnly(10, 0), -30));
    }

    [Fact]
    public void Between_Rejeita_Inicio_Posterior_Ao_Fim()
    {
        Assert.Throws<DomainRuleException>(() =>
            TimeRange.Between(new TimeOnly(18, 0), new TimeOnly(9, 0)));
    }

    [Fact]
    public void Overlaps_Detecta_Sobreposicao_Simples()
    {
        var first = TimeRange.From(new TimeOnly(10, 0), 60);   // 10:00-11:00
        var second = TimeRange.From(new TimeOnly(10, 30), 60); // 10:30-11:30

        Assert.True(first.Overlaps(second));
        Assert.True(second.Overlaps(first));
    }

    [Fact]
    public void Overlaps_Nao_Detecta_Intervalos_Adjacentes()
    {
        var first = TimeRange.From(new TimeOnly(10, 0), 60);  // 10:00-11:00
        var second = TimeRange.From(new TimeOnly(11, 0), 60); // 11:00-12:00

        Assert.False(first.Overlaps(second));
    }

    [Fact]
    public void Overlaps_Nao_Detecta_Intervalos_Disjuntos()
    {
        var first = TimeRange.From(new TimeOnly(10, 0), 30);  // 10:00-10:30
        var second = TimeRange.From(new TimeOnly(15, 0), 30); // 15:00-15:30

        Assert.False(first.Overlaps(second));
    }

    // ---------- DEF-001: intervalos que cruzam a meia-noite ----------

    [Fact]
    public void Overlaps_Detecta_Sobreposicao_Que_Cruza_Meia_Noite()
    {
        // Cenário do DEF-001: 23:50 + 30 min termina em 00:20 do dia seguinte.
        var existing = TimeRange.From(new TimeOnly(23, 50), 30); // 1430-1460
        var candidate = TimeRange.From(new TimeOnly(23, 55), 30); // 1435-1465

        Assert.True(existing.Overlaps(candidate));
        Assert.True(candidate.Overlaps(existing));
    }

    [Fact]
    public void From_Nao_Da_Volta_Na_Meia_Noite()
    {
        var range = TimeRange.From(new TimeOnly(23, 50), 30);

        // O fim é 1460 minutos (24:20 em minutos absolutos), não 20.
        Assert.Equal(1430, range.StartMinutes);
        Assert.Equal(1460, range.EndMinutes);
        Assert.Equal(30, range.DurationMinutes);
    }

    [Fact]
    public void IsContainedIn_Respeita_Limites()
    {
        var container = TimeRange.Between(new TimeOnly(9, 0), new TimeOnly(18, 0));

        Assert.True(TimeRange.From(new TimeOnly(9, 0), 60).IsContainedIn(container));
        Assert.True(TimeRange.From(new TimeOnly(17, 0), 60).IsContainedIn(container));
        Assert.False(TimeRange.From(new TimeOnly(17, 30), 60).IsContainedIn(container));
        Assert.False(TimeRange.From(new TimeOnly(8, 30), 60).IsContainedIn(container));
    }
}
