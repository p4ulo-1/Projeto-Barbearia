using Servia.Domain.Appointments;
using Servia.Domain.Common;

namespace Servia.Tests.Appointments;

/// <summary>
/// Regras R1 (só confirmado transiciona) e R2 (não concluir futuro).
/// Cobre os critérios F1-CA1, F2-CA1..3, F3-CA1..3, R1-CA1..3, R2-CA1..2,
/// E-CA1 e E-CA2.
/// </summary>
public class AppointmentTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid ServiceId = Guid.NewGuid();
    private static readonly Guid ProfessionalId = Guid.NewGuid();

    private static readonly DateOnly AppointmentDate = new(2026, 11, 9);
    private static readonly TimeOnly AppointmentTime = new(10, 0);
    private static readonly DateTimeOffset CreatedAt = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);

    private static Appointment NewAppointment() =>
        Appointment.Create(
            TenantId,
            CustomerId,
            ServiceId,
            ProfessionalId,
            AppointmentDate,
            AppointmentTime,
            durationMinutes: 30,
            now: CreatedAt);

    // ---------- F1-CA1: criação ----------

    [Fact]
    public void Criar_Agendamento_Valido_Inicia_Confirmado()
    {
        var appointment = NewAppointment();

        Assert.Equal(AppointmentStatus.Confirmed, appointment.Status);
        Assert.True(appointment.IsConfirmed);
        Assert.NotEqual(Guid.Empty, appointment.Id);
        Assert.Equal(CreatedAt, appointment.CreatedAt);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("customer")]
    [InlineData("service")]
    [InlineData("professional")]
    public void Criar_Agendamento_Sem_Referencia_Obrigatoria_Lanca_Excecao(string missing)
    {
        var tenantId = missing == "tenant" ? Guid.Empty : TenantId;
        var customerId = missing == "customer" ? Guid.Empty : CustomerId;
        var serviceId = missing == "service" ? Guid.Empty : ServiceId;
        var professionalId = missing == "professional" ? Guid.Empty : ProfessionalId;

        Assert.Throws<DomainRuleException>(() =>
            Appointment.Create(tenantId, customerId, serviceId, professionalId,
                AppointmentDate, AppointmentTime, 30, CreatedAt));
    }

    [Fact]
    public void Criar_Agendamento_Com_Data_Default_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            Appointment.Create(TenantId, CustomerId, ServiceId, ProfessionalId,
                default, AppointmentTime, 30, CreatedAt));
    }

    [Fact]
    public void Criar_Agendamento_Com_Duracao_Invalida_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            Appointment.Create(TenantId, CustomerId, ServiceId, ProfessionalId,
                AppointmentDate, AppointmentTime, 0, CreatedAt));
    }

    // ---------- F2 / R1: cancelamento ----------

    [Fact]
    public void Cancelar_Agendamento_Confirmado_Muda_Para_Cancelado()
    {
        var appointment = NewAppointment();

        appointment.Cancel("Cliente desistiu");

        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
        Assert.False(appointment.IsConfirmed);
        Assert.Equal("Cliente desistiu", appointment.CancellationReason);
    }

    [Fact]
    public void Cancelar_Agendamento_Ja_Cancelado_Lanca_Excecao()
    {
        var appointment = NewAppointment();
        appointment.Cancel();

        Assert.Throws<DomainRuleException>(() => appointment.Cancel());
    }

    [Fact]
    public void Cancelar_Agendamento_Ja_Cancelado_Preserva_Estado()
    {
        var appointment = NewAppointment();
        appointment.Cancel();

        Assert.Throws<DomainRuleException>(() => appointment.Cancel());

        // A invariante é preservada: o objeto continua em estado válido.
        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
        Assert.False(appointment.IsConfirmed);
    }

    // ---------- F3 / R2: conclusão ----------

    [Fact]
    public void Concluir_Atendimento_Passado_Muda_Para_Concluido()
    {
        var appointment = NewAppointment();
        var afterAppointment = new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

        appointment.Complete(afterAppointment);

        Assert.Equal(AppointmentStatus.Completed, appointment.Status);
    }

    [Fact]
    public void Concluir_Atendimento_Futuro_Lanca_Excecao()
    {
        var appointment = NewAppointment();
        var beforeAppointment = new DateTimeOffset(2026, 11, 9, 9, 0, 0, TimeSpan.Zero);

        var exception = Assert.Throws<DomainRuleException>(() => appointment.Complete(beforeAppointment));

        Assert.Contains("futuro", exception.Message);
        Assert.Equal(AppointmentStatus.Confirmed, appointment.Status);
    }

    [Fact]
    public void Concluir_Agendamento_Cancelado_Lanca_Excecao()
    {
        var appointment = NewAppointment();
        appointment.Cancel();
        var afterAppointment = new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

        Assert.Throws<DomainRuleException>(() => appointment.Complete(afterAppointment));
        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
    }

    // ---------- R1: remarcação ----------

    [Fact]
    public void Remarcar_Agendamento_Confirmado_Altera_Data_E_Hora()
    {
        var appointment = NewAppointment();
        var newDate = new DateOnly(2026, 11, 10);
        var newTime = new TimeOnly(14, 0);

        appointment.Reschedule(newDate, newTime);

        Assert.Equal(newDate, appointment.Date);
        Assert.Equal(newTime, appointment.Time);
        Assert.Equal(AppointmentStatus.Confirmed, appointment.Status);
    }

    [Fact]
    public void Remarcar_Agendamento_Cancelado_Lanca_Excecao()
    {
        var appointment = NewAppointment();
        appointment.Cancel();

        Assert.Throws<DomainRuleException>(() =>
            appointment.Reschedule(new DateOnly(2026, 11, 10), new TimeOnly(14, 0)));
    }

    [Fact]
    public void Remarcar_Com_Data_Default_Lanca_Excecao()
    {
        var appointment = NewAppointment();

        Assert.Throws<DomainRuleException>(() => appointment.Reschedule(default, new TimeOnly(14, 0)));
    }

    // ---------- A05 / E-CA2: estados e identidade ----------

    [Fact]
    public void Agendamento_Possui_Tres_Estados_Significativos()
    {
        var confirmed = NewAppointment();
        var cancelled = NewAppointment();
        cancelled.Cancel();
        var completed = NewAppointment();
        completed.Complete(new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero));

        var statuses = new[] { confirmed.Status, cancelled.Status, completed.Status };

        Assert.Equal(3, statuses.Distinct().Count());
    }

    [Fact]
    public void Transicoes_Preservam_Identidade()
    {
        var appointment = NewAppointment();
        var originalId = appointment.Id;

        appointment.Reschedule(new DateOnly(2026, 11, 10), new TimeOnly(14, 0));
        Assert.Equal(originalId, appointment.Id);

        appointment.Cancel();
        Assert.Equal(originalId, appointment.Id);
    }
}
