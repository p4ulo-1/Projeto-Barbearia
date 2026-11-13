using Servia.Application.Appointments;
using Servia.Domain.Appointments;
using Servia.Domain.Team;
using Servia.Domain.Tenancy;

namespace Servia.Tests.Appointments;

/// <summary>
/// Disponibilidade centralizada (C9).
///
/// <b>Regra:</b> o expediente do estabelecimento e a disponibilidade do
/// profissional são <b>independentes</b>. O agendamento depende só do
/// profissional; o expediente informa se o estabelecimento atende ao público.
/// </summary>
public sealed class AvailabilityServiceTests
{
    private static readonly DateOnly Monday = new(2026, 1, 5); // segunda-feira
    private static readonly Guid TenantId = Guid.NewGuid();

    private static Professional ProfessionalOn(params DayOfWeek[] days)
    {
        var schedule = WorkSchedule.Create(days, new TimeOnly(8, 0), new TimeOnly(18, 0), 30);
        return Professional.Create(TenantId, "Profissional", schedule);
    }

    private static Tenant TenantWithSchedule(EstablishmentSchedule schedule)
    {
        var tenant = Tenant.Create("teste", "Teste", SegmentType.Barbershop, "12345678000190", DateTimeOffset.UtcNow);
        tenant.UpdateSchedule(schedule);
        return tenant;
    }

    // ---------- Independência entre estabelecimento e profissional ----------

    [Fact]
    public async Task Estabelecimento_Aberto_Mesmo_Com_Todos_Profissionais_Indisponiveis()
    {
        // Estabelecimento aberto na segunda; NENHUM profissional trabalha na segunda.
        var schedule = EstablishmentSchedule.Create(BuildWeek(openDays: [DayOfWeek.Monday]));
        var repository = new FakeAvailabilityRepository();
        repository.SetTenant(TenantWithSchedule(schedule));
        repository.AddProfessional(ProfessionalOn(DayOfWeek.Friday));

        var service = new AvailabilityService(repository);

        Assert.True(await service.IsEstablishmentOpenAsync(TenantId, Monday));
    }

    [Fact]
    public async Task Estabelecimento_Sem_Expediente_Configurado_E_Fechado()
    {
        // BR5: sem expediente configurado, o estabelecimento nasce fechado
        // todos os dias. (Tenant.Create usa ClosedByDefault.)
        var repository = new FakeAvailabilityRepository();
        repository.SetTenant(Tenant.Create("teste", "Teste", SegmentType.Barbershop, "12345678000190", DateTimeOffset.UtcNow));

        var service = new AvailabilityService(repository);

        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            Assert.False(await service.IsEstablishmentOpenAsync(TenantId, Monday.AddDays((int)day)));
        }
    }

    [Fact]
    public async Task Estabelecimento_Fechado_Por_Padrao_Nao_Restringe_Agendamento()
    {
        // Sem expediente configurado (fechado), o profissional ainda agenda.
        var repository = new FakeAvailabilityRepository();
        repository.SetTenant(Tenant.Create("teste", "Teste", SegmentType.Barbershop, "12345678000190", DateTimeOffset.UtcNow));

        var professional = ProfessionalOn(DayOfWeek.Monday);
        repository.AddProfessional(professional);

        var service = new AvailabilityService(repository);

        Assert.False(await service.IsEstablishmentOpenAsync(TenantId, Monday));

        var check = await service.CheckSlotAsync(TenantId, professional.Id, Monday, new TimeOnly(9, 0), 30);
        Assert.True(check.IsAvailable);
    }

    [Fact]
    public async Task Profissional_Disponivel_Mesmo_Com_Estabelecimento_Fechado()
    {
        // Estabelecimento fechado na segunda; o profissional atende na segunda.
        var schedule = EstablishmentSchedule.Create(BuildWeek(openDays: []));
        var repository = new FakeAvailabilityRepository();
        repository.SetTenant(TenantWithSchedule(schedule));

        var professional = ProfessionalOn(DayOfWeek.Monday);
        repository.AddProfessional(professional);

        var service = new AvailabilityService(repository);

        // Estabelecimento fechado...
        Assert.False(await service.IsEstablishmentOpenAsync(TenantId, Monday));

        // ...mas o agendamento segue disponível (depende só do profissional).
        var slots = await service.GetAvailableSlotsAsync(TenantId, professional.Id, Monday, 30);
        Assert.NotEmpty(slots);

        var check = await service.CheckSlotAsync(TenantId, professional.Id, Monday, new TimeOnly(9, 0), 30);
        Assert.True(check.IsAvailable);
    }

    [Fact]
    public async Task Horario_Do_Estabelecimento_Nao_Restringe_Agendamento()
    {
        // Estabelecimento abre 09:00-10:00; profissional 08:00-18:00.
        var schedule = EstablishmentSchedule.Create(BuildWeek(
            openDays: [DayOfWeek.Monday],
            open: new TimeOnly(9, 0),
            close: new TimeOnly(10, 0)));
        var repository = new FakeAvailabilityRepository();
        repository.SetTenant(TenantWithSchedule(schedule));

        var professional = ProfessionalOn(DayOfWeek.Monday);
        repository.AddProfessional(professional);

        var service = new AvailabilityService(repository);

        // 16:00 está fora do expediente do estabelecimento, mas dentro da jornada do profissional.
        var check = await service.CheckSlotAsync(TenantId, professional.Id, Monday, new TimeOnly(16, 0), 30);
        Assert.True(check.IsAvailable);
    }

    // ---------- Expediente por dia ----------

    [Fact]
    public async Task Expediente_Configura_Dias_Diferentes_E_Dias_Fechados()
    {
        var schedule = EstablishmentSchedule.Create(
        [
            EstablishmentDaySchedule.Open(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0)),
            EstablishmentDaySchedule.Closed(DayOfWeek.Tuesday),
            EstablishmentDaySchedule.Open(DayOfWeek.Wednesday, new TimeOnly(14, 0), new TimeOnly(20, 0)),
            EstablishmentDaySchedule.Closed(DayOfWeek.Thursday),
            EstablishmentDaySchedule.Open(DayOfWeek.Friday, new TimeOnly(9, 0), new TimeOnly(18, 0)),
            EstablishmentDaySchedule.Closed(DayOfWeek.Saturday),
            EstablishmentDaySchedule.Closed(DayOfWeek.Sunday)
        ]);

        var repository = new FakeAvailabilityRepository();
        repository.SetTenant(TenantWithSchedule(schedule));
        var service = new AvailabilityService(repository);

        Assert.True(await service.IsEstablishmentOpenAsync(TenantId, Monday));
        Assert.False(await service.IsEstablishmentOpenAsync(TenantId, Monday.AddDays(1))); // terça
        Assert.True(await service.IsEstablishmentOpenAsync(TenantId, Monday.AddDays(2)));  // quarta

        // Horários diferentes por dia.
        var mondayHours = schedule.HoursOn(Monday);
        var wednesdayHours = schedule.HoursOn(Monday.AddDays(2));

        Assert.NotNull(mondayHours);
        Assert.NotNull(wednesdayHours);
        Assert.NotEqual(mondayHours!.Value, wednesdayHours!.Value);

        Assert.Null(schedule.HoursOn(Monday.AddDays(1))); // terça fechada
    }

    // ---------- Disponibilidade do profissional ----------

    [Fact]
    public async Task Horarios_Respeitam_Jornada_E_Duracao()
    {
        var repository = new FakeAvailabilityRepository();
        var professional = ProfessionalOn(DayOfWeek.Monday);
        repository.AddProfessional(professional);

        var service = new AvailabilityService(repository);

        var slots = await service.GetAvailableSlotsAsync(TenantId, professional.Id, Monday, 60);

        Assert.Equal(new TimeOnly(8, 0), slots.First());
        Assert.Equal(new TimeOnly(17, 0), slots.Last());
    }

    [Fact]
    public async Task Agendamento_Confirmado_Remove_O_Horario()
    {
        var repository = new FakeAvailabilityRepository();
        var professional = ProfessionalOn(DayOfWeek.Monday);
        repository.AddProfessional(professional);

        var appointment = Appointment.Create(
            TenantId, Guid.NewGuid(), Guid.NewGuid(), professional.Id,
            Monday, new TimeOnly(9, 0), 60, DateTimeOffset.UtcNow);
        repository.AddAppointment(appointment);

        var service = new AvailabilityService(repository);

        var slots = await service.GetAvailableSlotsAsync(TenantId, professional.Id, Monday, 60);

        Assert.DoesNotContain(new TimeOnly(9, 0), slots);
    }

    [Fact]
    public async Task Bloqueio_Remove_O_Horario()
    {
        var repository = new FakeAvailabilityRepository();
        var professional = ProfessionalOn(DayOfWeek.Monday);
        repository.AddProfessional(professional);

        repository.AddBlock(ScheduleBlock.Create(
            TenantId, professional.Id,
            new DateTimeOffset(Monday.ToDateTime(new TimeOnly(10, 0)), TimeSpan.Zero),
            new DateTimeOffset(Monday.ToDateTime(new TimeOnly(11, 0)), TimeSpan.Zero)));

        var service = new AvailabilityService(repository);

        var slots = await service.GetAvailableSlotsAsync(TenantId, professional.Id, Monday, 30);

        Assert.DoesNotContain(new TimeOnly(10, 0), slots);
    }

    [Fact]
    public async Task CheckSlot_Rejeita_Fora_Da_Jornada_Do_Profissional()
    {
        var repository = new FakeAvailabilityRepository();
        var professional = ProfessionalOn(DayOfWeek.Monday);
        repository.AddProfessional(professional);

        var service = new AvailabilityService(repository);

        var result = await service.CheckSlotAsync(
            TenantId, professional.Id, Monday, new TimeOnly(20, 0), 30);

        Assert.False(result.IsAvailable);
    }

    [Fact]
    public async Task CheckSlot_Rejeita_Dia_Sem_Trabalho_Do_Profissional()
    {
        var repository = new FakeAvailabilityRepository();
        var professional = ProfessionalOn(DayOfWeek.Monday);
        repository.AddProfessional(professional);

        var service = new AvailabilityService(repository);

        var result = await service.CheckSlotAsync(
            TenantId, professional.Id, Monday.AddDays(1), new TimeOnly(9, 0), 30);

        Assert.False(result.IsAvailable);
    }

    private static IEnumerable<EstablishmentDaySchedule> BuildWeek(
        DayOfWeek[] openDays,
        TimeOnly? open = null,
        TimeOnly? close = null)
    {
        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            yield return openDays.Contains(day)
                ? EstablishmentDaySchedule.Open(day, open ?? new TimeOnly(8, 0), close ?? new TimeOnly(18, 0))
                : EstablishmentDaySchedule.Closed(day);
        }
    }

    /// <summary>Repositório em memória para os testes de disponibilidade.</summary>
    private sealed class FakeAvailabilityRepository : IAvailabilityRepository
    {
        private readonly List<Professional> _professionals = [];
        private readonly List<Appointment> _appointments = [];
        private readonly List<ScheduleBlock> _blocks = [];
        private Tenant? _tenant;

        public void SetTenant(Tenant tenant) => _tenant = tenant;

        public void AddProfessional(Professional professional) => _professionals.Add(professional);

        public void AddAppointment(Appointment appointment) => _appointments.Add(appointment);

        public void AddBlock(ScheduleBlock block) => _blocks.Add(block);

        public Task<Tenant?> FindTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_tenant);

        public Task<Professional?> FindProfessionalAsync(
            Guid tenantId, Guid professionalId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_professionals.FirstOrDefault(
                professional => professional.TenantId == tenantId && professional.Id == professionalId));

        public Task<IReadOnlyList<Professional>> ListActiveProfessionalsAsync(
            Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Professional>>(
                _professionals.Where(professional => professional.TenantId == tenantId && professional.IsActive).ToList());

        public Task<IReadOnlyList<Appointment>> GetConfirmedAppointmentsAsync(
            Guid tenantId, Guid professionalId, DateOnly date, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Appointment>>(
                _appointments.Where(appointment => appointment.TenantId == tenantId
                    && appointment.ProfessionalId == professionalId
                    && appointment.Date == date
                    && appointment.IsConfirmed).ToList());

        public Task<IReadOnlyList<ScheduleBlock>> GetBlocksAsync(
            Guid tenantId, Guid professionalId, DateOnly date, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ScheduleBlock>>(
                _blocks.Where(block => block.TenantId == tenantId
                    && block.ProfessionalId == professionalId).ToList());
    }
}
