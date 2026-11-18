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

    /// <summary>
    /// Serviço vinculado ao profissional.
    ///
    /// A criação passou a exigir serviço ativo e vínculo profissional-serviço:
    /// a duração vem do serviço, não do pedido. Os testes que exercitam a
    /// regra de sobreposição usam este serviço como referência.
    /// </summary>
    private static Servia.Domain.Catalog.Service ServiceFor(
        FakeAppointmentRepository repository,
        int durationMinutes = 30)
    {
        var service = Servia.Domain.Catalog.Service.Create(
            TenantId, "Serviço", Money.From(50m), durationMinutes);

        // O comando usa o identificador estatico ServiceId; o servico
        // semeado precisa responder por ele.
        repository.SeedService(service, ServiceId);

        return service;
    }

    private static AppointmentService NewService(FakeAppointmentRepository repository) =>
        new(repository, new AvailabilityService(new LinkedAvailabilityRepository(repository)), new FixedTimeProvider(Now));

    private static CreateAppointmentCommand Command(TimeOnly time, int duration = 30) =>
        new(TenantId, CustomerId, ServiceId, ProfessionalId, Date, time);

    /// <summary>
    /// Semeia o serviço com a duração que o teste espera.
    ///
    /// A duração deixou de vir do pedido: ela é lida do serviço persistido.
    /// Para preservar o cenário de cada teste (sobreposição de 30, 60 ou 180
    /// minutos), o serviço é semeado com a duração correspondente.
    /// </summary>
    private static void SeedService(FakeAppointmentRepository repository, int durationMinutes) =>
        ServiceFor(repository, durationMinutes);

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
        SeedService(repository, 30);
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
        SeedService(repository, 30);
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
        SeedService(repository, 30);
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
        SeedService(repository, 60);
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
        SeedService(repository, 30);
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
        SeedService(repository, 30);
        var cancelled = Existing(new TimeOnly(10, 0), 60);
        cancelled.Cancel("Cliente desistiu");
        repository.Seed(cancelled);
        var service = NewService(repository);

        var result = await service.CreateAsync(Command(new TimeOnly(10, 30), 30));

        Assert.True(result.IsSuccess);
    }

    // ---------- R5-CA4: sobreposição que cruza a meia-noite (DEF-001) ----------

    [Fact]
    public void HasOverlap_Detecta_Sobreposicao_Que_Cruza_Meia_Noite()
    {
        // Cenário do DEF-001: 23:50 + 30 min termina em 00:20.
        // Usa a política de sessão longa, que permite ultrapassar a meia-noite.
        //
        // O teste exercita a regra de sobreposição diretamente: a criação
        // agora também valida a jornada, e uma jornada normal recusaria o
        // horário antes de chegar ao conflito. A regra de sobreposição em si
        // continua sendo a mesma, e é ela que este teste protege.
        var existing = Existing(new TimeOnly(23, 50), 30, new AllowOvernightPolicy());
        var candidate = TimeRange.From(new TimeOnly(23, 55), 30);

        Assert.True(AppointmentService.HasOverlap([existing], candidate));
    }

    // ---------- Isolamento por profissional e tenant ----------

    [Fact]
    public async Task Criar_Nao_Considera_Agendamento_De_Outro_Profissional()
    {
        var repository = new FakeAppointmentRepository();
        SeedService(repository, 30);
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
        SeedService(repository, 30);
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
        SeedService(repository, 30);
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
    public async Task Criar_Com_Referencia_Vazia_Retorna_Falha_De_Validacao()
    {
        var repository = new FakeAppointmentRepository();
        var service = NewService(repository);
        var command = new CreateAppointmentCommand(
            TenantId, Guid.Empty, ServiceId, ProfessionalId, Date, new TimeOnly(10, 0));

        var result = await service.CreateAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Equal(AppointmentFailureKind.Invalid, result.Failure!.Kind);
    }

    // ---------- Remarcação ----------

    [Fact]
    public async Task Remarcar_Para_Horario_Livre_Persiste()
    {
        var repository = new FakeAppointmentRepository();
        SeedService(repository, 30);
        var existing = Existing(new TimeOnly(10, 0), 30);
        repository.Seed(existing);
        var service = NewService(repository);

        var result = await service.RescheduleAsync(
            TenantId, existing.Id, Date, new TimeOnly(14, 0));

        Assert.True(result.IsSuccess);
        Assert.Equal(new TimeOnly(14, 0), result.Appointment!.Time);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task Remarcar_Para_O_Proprio_Intervalo_Nao_Gera_Falso_Conflito()
    {
        var repository = new FakeAppointmentRepository();
        SeedService(repository, 30);
        var existing = Existing(new TimeOnly(10, 0), 30);
        repository.Seed(existing);
        var service = NewService(repository);

        var result = await service.RescheduleAsync(
            TenantId, existing.Id, Date, new TimeOnly(10, 0));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Remarcar_Para_Horario_Ocupado_Falha_E_Nao_Altera_O_Original()
    {
        var repository = new FakeAppointmentRepository();
        SeedService(repository, 30);
        var target = Existing(new TimeOnly(14, 0), 30);
        var moving = Existing(new TimeOnly(10, 0), 30);
        repository.Seed(target);
        repository.Seed(moving);
        var service = NewService(repository);

        var result = await service.RescheduleAsync(
            TenantId, moving.Id, Date, new TimeOnly(14, 0));

        Assert.False(result.IsSuccess);
        Assert.Equal(AppointmentFailureKind.Conflict, result.Failure!.Kind);

        // O agendamento original permanece intacto.
        Assert.Equal(new TimeOnly(10, 0), moving.Time);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task Remarcar_Fora_Da_Jornada_Falha_E_Nao_Altera_O_Original()
    {
        var repository = new FakeAppointmentRepository();
        SeedService(repository, 30);
        var existing = Existing(new TimeOnly(10, 0), 30);
        repository.Seed(existing);
        var service = NewService(repository);

        // A jornada do dublê termina às 23:59; 23:50 + 30 min ultrapassa.
        var result = await service.RescheduleAsync(
            TenantId, existing.Id, Date, new TimeOnly(23, 50));

        Assert.False(result.IsSuccess);
        Assert.Equal(AppointmentFailureKind.Invalid, result.Failure!.Kind);
        Assert.Equal(new TimeOnly(10, 0), existing.Time);
    }

    [Fact]
    public async Task Remarcar_Agendamento_Inexistente_Retorna_NotFound()
    {
        var repository = new FakeAppointmentRepository();
        SeedService(repository, 30);
        var service = NewService(repository);

        var result = await service.RescheduleAsync(
            TenantId, Guid.NewGuid(), Date, new TimeOnly(10, 0));

        Assert.False(result.IsSuccess);
        Assert.Equal(AppointmentFailureKind.NotFound, result.Failure!.Kind);
    }

    [Fact]
    public async Task Remarcar_Agendamento_Cancelado_Falha()
    {
        var repository = new FakeAppointmentRepository();
        SeedService(repository, 30);
        var cancelled = Existing(new TimeOnly(10, 0), 30);
        cancelled.Cancel("Cliente desistiu");
        repository.Seed(cancelled);
        var service = NewService(repository);

        var result = await service.RescheduleAsync(
            TenantId, cancelled.Id, Date, new TimeOnly(14, 0));

        Assert.False(result.IsSuccess);
        Assert.Equal(AppointmentFailureKind.Invalid, result.Failure!.Kind);
    }

    [Fact]
    public async Task Remarcar_De_Outro_Tenant_Nao_Encontra()
    {
        var repository = new FakeAppointmentRepository();
        SeedService(repository, 30);
        var existing = Existing(new TimeOnly(10, 0), 30);
        repository.Seed(existing);
        var service = NewService(repository);

        var result = await service.RescheduleAsync(
            Guid.NewGuid(), existing.Id, Date, new TimeOnly(14, 0));

        Assert.False(result.IsSuccess);
        Assert.Equal(AppointmentFailureKind.NotFound, result.Failure!.Kind);
    }

    // ---------- Remarcação preserva a duração registrada ----------

    [Fact]
    public async Task Remarcar_Apos_Aumento_Da_Duracao_Do_Servico_Preserva_A_Duracao_Original()
    {
        // O agendamento foi criado com 30 minutos. Depois, o serviço passou a
        // 60. A remarcação valida e preserva os 30 do agendamento.
        var repository = new FakeAppointmentRepository();
        var service = ServiceFor(repository, durationMinutes: 30);
        var existing = Existing(new TimeOnly(10, 0), 30);
        repository.Seed(existing);
        var appointmentService = NewService(repository);

        service.ChangeDuration(60);

        var result = await appointmentService.RescheduleAsync(
            TenantId, existing.Id, Date, new TimeOnly(14, 0));

        Assert.True(result.IsSuccess);
        Assert.Equal(30, result.Appointment!.DurationMinutes);
    }

    [Fact]
    public async Task Remarcar_Apos_Reducao_Da_Duracao_Do_Servico_Preserva_A_Duracao_Original()
    {
        // O agendamento foi criado com 60 minutos. Depois, o serviço passou a
        // 30. A remarcação preserva os 60 do agendamento.
        var repository = new FakeAppointmentRepository();
        var service = ServiceFor(repository, durationMinutes: 60);
        var existing = Existing(new TimeOnly(10, 0), 60);
        repository.Seed(existing);
        var appointmentService = NewService(repository);

        service.ChangeDuration(30);

        var result = await appointmentService.RescheduleAsync(
            TenantId, existing.Id, Date, new TimeOnly(14, 0));

        Assert.True(result.IsSuccess);
        Assert.Equal(60, result.Appointment!.DurationMinutes);
    }

    [Fact]
    public async Task Remarcar_Detecta_Conflito_Pela_Duracao_Original_E_Nao_Pela_Atual()
    {
        // Agendamento de 60 min (10:00-11:00). O serviço foi reduzido para 30.
        // Um agendamento ocupa 10:30-11:00. Se a remarcação usasse a duração
        // atual (30), 10:00-10:30 pareceria livre — mas o agendamento ocupa
        // 60 minutos e conflita.
        var repository = new FakeAppointmentRepository();
        var service = ServiceFor(repository, durationMinutes: 60);
        var moving = Existing(new TimeOnly(10, 0), 60);
        var blocker = Existing(new TimeOnly(10, 30), 30);
        repository.Seed(moving);
        repository.Seed(blocker);
        var appointmentService = NewService(repository);

        service.ChangeDuration(30);

        var result = await appointmentService.RescheduleAsync(
            TenantId, moving.Id, Date, new TimeOnly(10, 0));

        Assert.False(result.IsSuccess);
        Assert.Equal(AppointmentFailureKind.Conflict, result.Failure!.Kind);
        Assert.Equal(new TimeOnly(10, 0), moving.Time);
    }

    [Fact]
    public async Task Remarcar_Nao_Altera_A_Duracao_Registrada()
    {
        var repository = new FakeAppointmentRepository();
        var service = ServiceFor(repository, durationMinutes: 45);
        var existing = Existing(new TimeOnly(10, 0), 45);
        repository.Seed(existing);
        var appointmentService = NewService(repository);

        var result = await appointmentService.RescheduleAsync(
            TenantId, existing.Id, Date, new TimeOnly(15, 0));

        Assert.True(result.IsSuccess);
        Assert.Equal(45, result.Appointment!.DurationMinutes);
        Assert.Equal(new TimeOnly(15, 0), result.Appointment.Time);
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

