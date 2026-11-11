using Servia.Application.Appointments;
using Servia.Domain.Appointments;
using Servia.Domain.Common;
using Servia.Domain.Policies;

using Servia.Tests.Common;

namespace Servia.Tests.Appointments;

/// <summary>
/// Regra R5 — não pode haver sobreposição de agendamentos ativos.
/// Cobre os critérios R5-CA1..4 e F1-CA4.
/// </summary>
public class AppointmentServiceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid ServiceId = Guid.NewGuid();
    private static readonly Guid ProfessionalId = Guid.NewGuid();
    private static readonly DateOnly Date = new(2026, 11, 9);

    private static readonly DateTimeOffset Now = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);

    private static AppointmentService NewService(FakeAppointmentRepository repository) =>
        new(repository, new FixedTimeProvider(Now));

    private static CreateAppointmentCommand Command(TimeOnly time, int duration = 30) =>
        new(TenantId, CustomerId, ServiceId, ProfessionalId, Date, time, duration);

    private static Appointment Existing(
        TimeOnly time,
        int duration = 30,
        IOvernightPolicy? policy = null) =>
        Appointment.Create(TenantId, CustomerId, ServiceId, ProfessionalId, Date, time, duration, Now, policy);

    // ---------- R5-CA2: horário livre ----------

    [Fact]
    public async Task Criar_Aceita_Horario_Livre()
    {
        var repository = new FakeAppointmentRepository();
        var service = NewService(repository);

        var result = await service.CreateAsync(Command(new TimeOnly(10, 0)));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Appointment);
        Assert.Equal(AppointmentStatus.Confirmed, result.Appointment!.Status);
        Assert.Single(repository.All);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task Criar_Aceita_Horario_Adjacente_Sem_Sobreposicao()
    {
        var repository = new FakeAppointmentRepository();
        repository.Seed(Existing(new TimeOnly(10, 0), 30)); // 10:00-10:30
        var service = NewService(repository);

        var result = await service.CreateAsync(Command(new TimeOnly(10, 30), 30)); // 10:30-11:00

        Assert.True(result.IsSuccess);
    }

    // ---------- R5-CA1: sobreposição ----------

    [Fact]
    public async Task Criar_Rejeita_Sobreposicao_De_Horario()
    {
        var repository = new FakeAppointmentRepository();
        repository.Seed(Existing(new TimeOnly(10, 0), 60)); // 10:00-11:00
        var service = NewService(repository);

        var result = await service.CreateAsync(Command(new TimeOnly(10, 30), 30)); // 10:30-11:00

        Assert.False(result.IsSuccess);
        Assert.Equal(AppointmentFailureKind.Conflict, result.Failure!.Kind);
        Assert.Single(repository.All); // nada foi adicionado
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task Criar_Rejeita_Sobreposicao_Quando_Novo_Contem_Existente()
    {
        var repository = new FakeAppointmentRepository();
        repository.Seed(Existing(new TimeOnly(10, 15), 15)); // 10:15-10:30
        var service = NewService(repository);

        var result = await service.CreateAsync(Command(new TimeOnly(10, 0), 60)); // 10:00-11:00

        Assert.False(result.IsSuccess);
        Assert.Equal(AppointmentFailureKind.Conflict, result.Failure!.Kind);
    }

    [Fact]
    public async Task Criar_Rejeita_Sobreposicao_Quando_Existente_Contem_Novo()
    {
        var repository = new FakeAppointmentRepository();
        repository.Seed(Existing(new TimeOnly(9, 0), 180)); // 09:00-12:00
        var service = NewService(repository);

        var result = await service.CreateAsync(Command(new TimeOnly(10, 0), 30));

        Assert.False(result.IsSuccess);
        Assert.Equal(AppointmentFailureKind.Conflict, result.Failure!.Kind);
    }

    // ---------- R5-CA3: cancelado não bloqueia ----------

    [Fact]
    public async Task Criar_Ignora_Agendamento_Cancelado()
    {
        var repository = new FakeAppointmentRepository();
        var cancelled = Existing(new TimeOnly(10, 0), 60);
        cancelled.Cancel("Cliente desistiu");
        repository.Seed(cancelled);
        var service = NewService(repository);

        var result = await service.CreateAsync(Command(new TimeOnly(10, 30), 30));

        Assert.True(result.IsSuccess);
    }

    // ---------- R5-CA4: sobreposição que cruza a meia-noite (DEF-001) ----------

    [Fact]
    public async Task HasOverlap_Detecta_Sobreposicao_Que_Cruza_Meia_Noite()
    {
        // Cenário do DEF-001: 23:50 + 30 min termina em 00:20.
        // Usa a política de sessão longa, que permite ultrapassar a meia-noite.
        var repository = new FakeAppointmentRepository();
        repository.Seed(Existing(new TimeOnly(23, 50), 30, new AllowOvernightPolicy()));
        var service = NewService(repository);

        var result = await service.CreateAsync(Command(new TimeOnly(23, 55), 30));

        Assert.False(result.IsSuccess);
        Assert.Equal(AppointmentFailureKind.Conflict, result.Failure!.Kind);
    }

    // ---------- Isolamento por profissional e tenant ----------

    [Fact]
    public async Task Criar_Nao_Considera_Agendamento_De_Outro_Profissional()
    {
        var repository = new FakeAppointmentRepository();
        var otherProfessional = Appointment.Create(
            TenantId, CustomerId, ServiceId, Guid.NewGuid(), Date, new TimeOnly(10, 0), 60, Now);
        repository.Seed(otherProfessional);
        var service = NewService(repository);

        var result = await service.CreateAsync(Command(new TimeOnly(10, 30), 30));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Criar_Nao_Considera_Agendamento_De_Outro_Tenant()
    {
        var repository = new FakeAppointmentRepository();
        var otherTenant = Appointment.Create(
            Guid.NewGuid(), CustomerId, ServiceId, ProfessionalId, Date, new TimeOnly(10, 0), 60, Now);
        repository.Seed(otherTenant);
        var service = NewService(repository);

        var result = await service.CreateAsync(Command(new TimeOnly(10, 30), 30));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Criar_Nao_Considera_Agendamento_De_Outra_Data()
    {
        var repository = new FakeAppointmentRepository();
        var otherDate = Appointment.Create(
            TenantId, CustomerId, ServiceId, ProfessionalId,
            new DateOnly(2026, 11, 10), new TimeOnly(10, 0), 60, Now);
        repository.Seed(otherDate);
        var service = NewService(repository);

        var result = await service.CreateAsync(Command(new TimeOnly(10, 30), 30));

        Assert.True(result.IsSuccess);
    }

    // ---------- Validação de entrada ----------

    [Fact]
    public async Task Criar_Com_Duracao_Invalida_Retorna_Falha_De_Validacao()
    {
        var repository = new FakeAppointmentRepository();
        var service = NewService(repository);

        var result = await service.CreateAsync(Command(new TimeOnly(10, 0), 0));

        Assert.False(result.IsSuccess);
        Assert.Equal(AppointmentFailureKind.Invalid, result.Failure!.Kind);
        Assert.Empty(repository.All);
    }

    [Fact]
    public async Task Criar_Com_Referencia_Vazia_Retorna_Falha_De_Validacao()
    {
        var repository = new FakeAppointmentRepository();
        var service = NewService(repository);
        var command = new CreateAppointmentCommand(
            TenantId, Guid.Empty, ServiceId, ProfessionalId, Date, new TimeOnly(10, 0), 30);

        var result = await service.CreateAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Equal(AppointmentFailureKind.Invalid, result.Failure!.Kind);
    }

    // ---------- HasOverlap isolado ----------

    [Fact]
    public void HasOverlap_Retorna_Falso_Sem_Agendamentos()
    {
        var candidate = TimeRange.From(new TimeOnly(10, 0), 30);

        Assert.False(AppointmentService.HasOverlap([], candidate));
    }

    [Fact]
    public void HasOverlap_Ignora_Agendamento_Cancelado()
    {
        var cancelled = Existing(new TimeOnly(10, 0), 60);
        cancelled.Cancel();
        var candidate = TimeRange.From(new TimeOnly(10, 30), 30);

        Assert.False(AppointmentService.HasOverlap([cancelled], candidate));
    }

    [Fact]
    public void HasOverlap_Detecta_Conflito_Com_Agendamento_Confirmado()
    {
        var confirmed = Existing(new TimeOnly(10, 0), 60);
        var candidate = TimeRange.From(new TimeOnly(10, 30), 30);

        Assert.True(AppointmentService.HasOverlap([confirmed], candidate));
    }
}

