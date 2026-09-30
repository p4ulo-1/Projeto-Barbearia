using BackAndre.Domain.Models;

namespace BackAndre.Tests;

public class BarberScheduleTests
{
    [Fact]
    public void Constructor_StartEqualToEnd_ThrowsDomainRuleException()
    {
        Assert.Throws<DomainRuleException>(() =>
            new BarberSchedule([2, 3, 4], new TimeOnly(9, 0), new TimeOnly(9, 0)));
    }

    [Fact]
    public void Constructor_StartAfterEnd_ThrowsDomainRuleException()
    {
        Assert.Throws<DomainRuleException>(() =>
            new BarberSchedule([2, 3, 4], new TimeOnly(18, 0), new TimeOnly(9, 0)));
    }

    [Fact]
    public void WorksOn_WorkingDay_ReturnsTrue()
    {
        var schedule = CreateSchedule();

        Assert.True(schedule.WorksOn(new DateOnly(2026, 9, 29))); // terça-feira
    }

    [Fact]
    public void WorksOn_DayOff_ReturnsFalse()
    {
        var schedule = CreateSchedule();

        Assert.False(schedule.WorksOn(new DateOnly(2026, 9, 27))); // domingo
    }

    [Fact]
    public void FitsWithinSchedule_ServiceEndingAtClosingTime_ReturnsTrue()
    {
        var schedule = CreateSchedule();

        Assert.True(schedule.FitsWithinSchedule(new TimeOnly(19, 30), 30));
    }

    [Fact]
    public void FitsWithinSchedule_ServiceEndingAfterClosingTime_ReturnsFalse()
    {
        var schedule = CreateSchedule();

        Assert.False(schedule.FitsWithinSchedule(new TimeOnly(19, 31), 30));
    }

    [Fact]
    public void FitsWithinSchedule_ServiceStartingBeforeOpening_ReturnsFalse()
    {
        var schedule = CreateSchedule();

        Assert.False(schedule.FitsWithinSchedule(new TimeOnly(8, 59), 30));
    }

    private static BarberSchedule CreateSchedule() =>
        new([2, 3, 4, 5, 6], new TimeOnly(9, 0), new TimeOnly(20, 0));
}
