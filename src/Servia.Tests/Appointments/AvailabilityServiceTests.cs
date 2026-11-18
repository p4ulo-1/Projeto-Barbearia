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

    /// <summary>
    /// Serviço vinculado ao profissional.
    ///
    /// A disponibilidade depende do serviço: sem vínculo, não há horário a
    /// oferecer. Os testes que exercitam jornada, bloqueio e conflito usam
    /// este serviço como referência.
    /// </summary>
    private static Servia.Domain.Catalog.Service ServiceFor(
        FakeAvailabilityRepository repository,
        Professional professional,
        int durationMinutes = 30)
    {
        var service = Servia.Domain.Catalog.Service.Create(
            TenantId, "Serviço", Servia.Domain.Common.Money.From(50m), durationMinutes);

        repository.AddService(service);
        repository.LinkService(professional.Id, service.Id);

        return service;
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
        var catalogService = ServiceFor(repository, professional);

        var service = new AvailabilityService(repository);

        Assert.False(await service.IsEstablishmentOpenAsync(TenantId, Monday));

        var check = await service.CheckSlotAsync(
            TenantId, professional.Id, catalogService.Id, Monday, new TimeOnly(9, 0));
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
        var catalogService = ServiceFor(repository, professional);

        var service = new AvailabilityService(repository);

        // Estabelecimento fechado...
        Assert.False(await service.IsEstablishmentOpenAsync(TenantId, Monday));

        // ...mas o agendamento segue disponível (depende só do profissional).
        var slots = await service.GetAvailableSlotsAsync(
            TenantId, professional.Id, catalogService.Id, Monday);
        Assert.NotEmpty(slots);

        var check = await service.CheckSlotAsync(
            TenantId, professional.Id, catalogService.Id, Monday, new TimeOnly(9, 0));
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
        var catalogService = ServiceFor(repository, professional);

        var service = new AvailabilityService(repository);

        // 16:00 está fora do expediente do estabelecimento, mas dentro da jornada do profissional.
        var check = await service.CheckSlotAsync(
            TenantId, professional.Id, catalogService.Id, Monday, new TimeOnly(16, 0));
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
        var catalogService = ServiceFor(repository, professional, durationMinutes: 60);

        var service = new AvailabilityService(repository);

        var slots = await service.GetAvailableSlotsAsync(
            TenantId, professional.Id, catalogService.Id, Monday);

        Assert.Equal(new TimeOnly(8, 0), slots.First());
        Assert.Equal(new TimeOnly(17, 0), slots.Last());
    }

    [Fact]
    public async Task Agendamento_Confirmado_Remove_O_Horario()
    {
        var repository = new FakeAvailabilityRepository();
        var professional = ProfessionalOn(DayOfWeek.Monday);
        repository.AddProfessional(professional);
        var catalogService = ServiceFor(repository, professional, durationMinutes: 60);

        var appointment = Appointment.Create(
            TenantId, Guid.NewGuid(), catalogService.Id, professional.Id,
            Monday, new TimeOnly(9, 0), 60, DateTimeOffset.UtcNow);
        repository.AddAppointment(appointment);

        var service = new AvailabilityService(repository);

        var slots = await service.GetAvailableSlotsAsync(
            TenantId, professional.Id, catalogService.Id, Monday);

        Assert.DoesNotContain(new TimeOnly(9, 0), slots);
    }

    [Fact]
    public async Task Bloqueio_Remove_O_Horario()
    {
        var repository = new FakeAvailabilityRepository();
        var professional = ProfessionalOn(DayOfWeek.Monday);
        repository.AddProfessional(professional);
        var catalogService = ServiceFor(repository, professional);

        repository.AddBlock(ScheduleBlock.Create(
            TenantId, professional.Id,
            new DateTimeOffset(Monday.ToDateTime(new TimeOnly(10, 0)), TimeSpan.Zero),
            new DateTimeOffset(Monday.ToDateTime(new TimeOnly(11, 0)), TimeSpan.Zero)));

        var service = new AvailabilityService(repository);

        var slots = await service.GetAvailableSlotsAsync(
            TenantId, professional.Id, catalogService.Id, Monday);

        Assert.DoesNotContain(new TimeOnly(10, 0), slots);
    }

    [Fact]
    public async Task CheckSlot_Rejeita_Fora_Da_Jornada_Do_Profissional()
    {
        var repository = new FakeAvailabilityRepository();
        var professional = ProfessionalOn(DayOfWeek.Monday);
        repository.AddProfessional(professional);
        var catalogService = ServiceFor(repository, professional);

        var service = new AvailabilityService(repository);

        var result = await service.CheckSlotAsync(
            TenantId, professional.Id, catalogService.Id, Monday, new TimeOnly(20, 0));

        Assert.False(result.IsAvailable);
    }

    [Fact]
    public async Task CheckSlot_Rejeita_Dia_Sem_Trabalho_Do_Profissional()
    {
        var repository = new FakeAvailabilityRepository();
        var professional = ProfessionalOn(DayOfWeek.Monday);
        repository.AddProfessional(professional);
        var catalogService = ServiceFor(repository, professional);

        var service = new AvailabilityService(repository);

        var result = await service.CheckSlotAsync(
            TenantId, professional.Id, catalogService.Id, Monday.AddDays(1), new TimeOnly(9, 0));

        Assert.False(result.IsAvailable);
    }

    // ---------- Serviço e vínculo profissional-serviço ----------

    [Fact]
    public async Task Profissional_Sem_Vinculo_Nao_Tem_Horario_Para_O_Servico()
    {
        // O profissional existe e trabalha na segunda, mas não executa o
        // serviço. Não há horário a oferecer: a consulta não pode apresentar
        // alguém que não realiza o atendimento.
        var repository = new FakeAvailabilityRepository();
        var professional = ProfessionalOn(DayOfWeek.Monday);
        repository.AddProfessional(professional);

        var service = Servia.Domain.Catalog.Service.Create(
            TenantId, "Serviço", Servia.Domain.Common.Money.From(50m), 30);
        repository.AddService(service);

        var availability = new AvailabilityService(repository);

        var slots = await availability.GetAvailableSlotsAsync(
            TenantId, professional.Id, service.Id, Monday);

        Assert.Empty(slots);

        var check = await availability.CheckSlotAsync(
            TenantId, professional.Id, service.Id, Monday, new TimeOnly(9, 0));

        Assert.False(check.IsAvailable);
        Assert.False(check.IsConflict);
    }

    [Fact]
    public async Task Servico_Inativo_Nao_Tem_Horario()
    {
        var repository = new FakeAvailabilityRepository();
        var professional = ProfessionalOn(DayOfWeek.Monday);
        repository.AddProfessional(professional);
        var service = ServiceFor(repository, professional);
        service.Deactivate();

        var availability = new AvailabilityService(repository);

        var slots = await availability.GetAvailableSlotsAsync(
            TenantId, professional.Id, service.Id, Monday);

        Assert.Empty(slots);
    }

    [Fact]
    public async Task Servico_Inexistente_Nao_Tem_Horario()
    {
        var repository = new FakeAvailabilityRepository();
        var professional = ProfessionalOn(DayOfWeek.Monday);
        repository.AddProfessional(professional);

        var availability = new AvailabilityService(repository);

        var slots = await availability.GetAvailableSlotsAsync(
            TenantId, professional.Id, Guid.NewGuid(), Monday);

        Assert.Empty(slots);
    }

    [Fact]
    public async Task Profissional_Inativo_Nao_Tem_Horario()
    {
        var repository = new FakeAvailabilityRepository();
        var professional = ProfessionalOn(DayOfWeek.Monday);
        repository.AddProfessional(professional);
        var service = ServiceFor(repository, professional);
        professional.Deactivate();

        var availability = new AvailabilityService(repository);

        var slots = await availability.GetAvailableSlotsAsync(
            TenantId, professional.Id, service.Id, Monday);

        Assert.Empty(slots);
    }

    [Fact]
    public async Task Duracao_Vem_Do_Servico_E_Define_Os_Horarios()
    {
        // Serviço de 60 minutos: o último horário que cabe na jornada
        // 08:00–18:00 é 17:00. Com 30 minutos, seria 17:30.
        var repository = new FakeAvailabilityRepository();
        var professional = ProfessionalOn(DayOfWeek.Monday);
        repository.AddProfessional(professional);
        var longService = ServiceFor(repository, professional, durationMinutes: 60);

        var availability = new AvailabilityService(repository);

        var slots = await availability.GetAvailableSlotsAsync(
            TenantId, professional.Id, longService.Id, Monday);

        Assert.Equal(new TimeOnly(17, 0), slots.Last());
    }

    [Fact]
    public async Task Remarcacao_Ignora_O_Proprio_Agendamento()
    {
        // O agendamento das 09:00 não pode conflitar consigo mesmo ao ser
        // remarcado para o mesmo intervalo.
        var repository = new FakeAvailabilityRepository();
        var professional = ProfessionalOn(DayOfWeek.Monday);
        repository.AddProfessional(professional);
        var service = ServiceFor(repository, professional);

        var appointment = Appointment.Create(
            TenantId, Guid.NewGuid(), service.Id, professional.Id,
            Monday, new TimeOnly(9, 0), 30, DateTimeOffset.UtcNow);
        repository.AddAppointment(appointment);

        var availability = new AvailabilityService(repository);

        var withSelf = await availability.CheckSlotAsync(
            TenantId, professional.Id, service.Id, Monday, new TimeOnly(9, 0));
        Assert.False(withSelf.IsAvailable);
        Assert.True(withSelf.IsConflict);

        var excludingSelf = await availability.EvaluateAsync(
            TenantId, professional.Id, service.Id, Monday, new TimeOnly(9, 0),
            excludeAppointmentId: appointment.Id);

        Assert.True(excludingSelf.Availability.IsAvailable);
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
        private readonly List<Servia.Domain.Catalog.Service> _services = [];
        private readonly List<ProfessionalService> _links = [];
        private Tenant? _tenant;

        public void SetTenant(Tenant tenant) => _tenant = tenant;

        public void AddProfessional(Professional professional) => _professionals.Add(professional);

        public void AddService(Servia.Domain.Catalog.Service service) => _services.Add(service);

        public void LinkService(Guid professionalId, Guid serviceId) =>
            _links.Add(ProfessionalService.Create(TenantId, professionalId, serviceId));

        public void AddAppointment(Appointment appointment) => _appointments.Add(appointment);

        public void AddBlock(ScheduleBlock block) => _blocks.Add(block);

        public Task<Tenant?> FindTenantAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_tenant);

        public Task<Professional?> FindProfessionalAsync(
            Guid tenantId, Guid professionalId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_professionals.FirstOrDefault(
                professional => professional.TenantId == tenantId && professional.Id == professionalId));

        public Task<Servia.Domain.Catalog.Service?> FindServiceAsync(
            Guid tenantId, Guid serviceId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_services.FirstOrDefault(
                service => service.TenantId == tenantId && service.Id == serviceId));

        public Task<bool> IsServiceLinkedAsync(
            Guid tenantId, Guid professionalId, Guid serviceId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_links.Any(link => link.TenantId == tenantId
                && link.ProfessionalId == professionalId
                && link.ServiceId == serviceId));

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
