using BackAndre.Models;

namespace BackAndre.Tests;

public class AppointmentTests
{
    [Fact]
    public void Constructor_CreatesConfirmedAppointmentAndDefinesCreatedAt()
    {
        var before = DateTime.UtcNow;

        var appointment = CreateAppointment();

        Assert.Equal(Appointment.ConfirmedStatus, appointment.Status);
        Assert.InRange(appointment.CreatedAt, before, DateTime.UtcNow);
    }

    [Fact]
    public void Cancel_ConfirmedAppointment_ChangesStatus()
    {
        var appointment = CreateAppointment();

        appointment.Cancel();

        Assert.Equal(Appointment.CanceledStatus, appointment.Status);
    }

    [Fact]
    public void Cancel_CanceledAppointment_ThrowsDomainRuleException()
    {
        var appointment = CreateAppointment();
        appointment.Cancel();

        Assert.Throws<DomainRuleException>(() => appointment.Cancel());
    }

    [Fact]
    public void Complete_ConfirmedPastAppointment_ChangesStatus()
    {
        var appointment = CreateAppointment();

        appointment.Complete(new DateTime(2026, 10, 10, 11, 0, 0));

        Assert.Equal(Appointment.CompletedStatus, appointment.Status);
    }

    [Fact]
    public void Complete_CanceledAppointment_ThrowsDomainRuleException()
    {
        var appointment = CreateAppointment();
        appointment.Cancel();

        Assert.Throws<DomainRuleException>(() =>
            appointment.Complete(new DateTime(2026, 10, 10, 11, 0, 0)));
    }

    [Fact]
    public void Complete_FutureAppointment_ThrowsDomainRuleException()
    {
        var appointment = CreateAppointment();

        Assert.Throws<DomainRuleException>(() =>
            appointment.Complete(new DateTime(2026, 10, 9, 11, 0, 0)));
    }

    [Fact]
    public void Reschedule_ConfirmedAppointment_ChangesDateAndTime()
    {
        var appointment = CreateAppointment();
        var newDate = new DateOnly(2026, 10, 12);
        var newTime = new TimeOnly(14, 30);

        appointment.Reschedule(newDate, newTime);

        Assert.Equal(newDate, appointment.Date);
        Assert.Equal(newTime, appointment.Time);
        Assert.Equal(Appointment.ConfirmedStatus, appointment.Status);
    }

    [Fact]
    public void Reschedule_CanceledAppointment_ThrowsDomainRuleException()
    {
        var appointment = CreateAppointment();
        appointment.Cancel();

        Assert.Throws<DomainRuleException>(() =>
            appointment.Reschedule(new DateOnly(2026, 10, 12), new TimeOnly(14, 30)));
    }

    private static Appointment CreateAppointment() => new(
        "user-1",
        "service-1",
        "barber-1",
        new DateOnly(2026, 10, 10),
        new TimeOnly(10, 0));
}
