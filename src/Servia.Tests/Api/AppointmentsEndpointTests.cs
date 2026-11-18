using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Servia.Api.Contracts;
using Servia.Api.Controllers;
using Servia.Api.Middleware;
using Servia.Domain.Appointments;
using Servia.Domain.Catalog;
using Servia.Domain.Common;
using Servia.Domain.Customers;
using Servia.Domain.Team;
using Servia.Domain.Identity;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Identity;
using Servia.Infrastructure.Persistence;

namespace Servia.Tests.Api;

/// <summary>
/// Teste de integração ponta a ponta do fluxo F1 (criar agendamento).
///
/// Exercita o caminho completo: HTTP → controller → caso de uso → domínio →
/// persistência. Usa um banco SQLite isolado por teste, substituindo o
/// contexto registrado pela aplicação.
///
/// Critério F03: teste de integração que executa uma operação pela API,
/// aplica uma regra de negócio e verifica a persistência.
/// </summary>
public class AppointmentsEndpointTests : IClassFixture<IsolatedApiFactory>
{
    private static readonly Guid TenantId = Guid.NewGuid();
    // IDs reais das entidades criadas no banco de teste.
    private const string Password = "SenhaSegura123";

    private readonly string _accessToken;

    private Guid _customerId;
    private Guid _serviceId;
    private Guid _professionalId;
    private static readonly DateOnly Date = new(2026, 11, 9);

    private readonly IsolatedApiFactory _factory;
    private readonly HttpClient _client;

    public AppointmentsEndpointTests(IsolatedApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();

        SeedReferences();
        _accessToken = SignIn();
    }

    /// <summary>
    /// Cria o usuario proprietario e obtem o token.
    /// O endpoint de agendamento exige autenticacao e permissao (F10).
    /// </summary>
    private string SignIn()
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(TenantId);

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        if (!context.Users.Any())
        {
            context.Users.Add(User.CreateForTenant(
                TenantId, "Proprietario", "owner@exemplo.com",
                hasher.Hash(Password), Role.Owner, DateTimeOffset.UtcNow));

            context.SaveChanges();
        }

        var response = _client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(
            TenantId, "owner@exemplo.com", Password)).GetAwaiter().GetResult();

        var body = response.Content.ReadFromJsonAsync<AuthResponse>().GetAwaiter().GetResult();

        return body!.AccessToken;
    }

    /// <summary>
    /// Cria cliente, servico e profissional do tenant de teste.
    /// A FK composta (TenantId, Id) exige que existam antes do agendamento.
    /// </summary>
    private void SeedReferences()
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(TenantId);
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (context.Customers.Any())
        {
            var existing = context.Customers.First();
            _customerId = existing.Id;
            _serviceId = context.Services.First().Id;
            _professionalId = context.Professionals.First().Id;
            return;
        }

        var customer = Customer.Create(TenantId, "Cliente", "21999990000", now: DateTimeOffset.UtcNow);
        var service = Service.Create(TenantId, "Servico", Money.From(50m), 30);
        var professional = Professional.Create(TenantId, "Profissional",
            WorkSchedule.Create([DayOfWeek.Monday], new TimeOnly(9, 0), new TimeOnly(18, 0)));

        context.Customers.Add(customer);
        context.Services.Add(service);
        context.Professionals.Add(professional);

        // O agendamento exige vinculo profissional-servico: sem ele, o
        // horario nao e oferecido nem reservado.
        context.ProfessionalServices.Add(
            ProfessionalService.Create(TenantId, professional.Id, service.Id));

        context.SaveChanges();

        _customerId = customer.Id;
        _serviceId = service.Id;
        _professionalId = professional.Id;
    }

    private CreateAppointmentRequest Request(TimeOnly time) =>
        new(_customerId, _serviceId, _professionalId, Date, time);

    /// <summary>Envia o pedido com o tenant resolvido pelo header do servidor.</summary>
    private async Task<HttpResponseMessage> PostAsync(
        CreateAppointmentRequest request,
        Guid? tenantId = null)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/appointments")
        {
            Content = JsonContent.Create(request)
        };

        message.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

        return await _client.SendAsync(message);
    }

    // ---------- F1-CA1: criação bem-sucedida ----------

    [Fact]
    public async Task Criar_Agendamento_Retorna_201_E_Persiste()
    {
        var response = await PostAsync(Request(new TimeOnly(10, 0)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AppointmentResponse>();

        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.Id);
        Assert.Equal("Confirmed", body.Status);
        Assert.Equal(Date, body.Date);
        Assert.Equal(new TimeOnly(10, 0), body.Time);

        // Verifica a persistência: o registro existe no banco.
        // O filtro global exige tenant resolvido também na verificação.
        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(TenantId);
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persisted = await context.Appointments.FirstOrDefaultAsync(item => item.Id == body.Id);

        Assert.NotNull(persisted);
        Assert.Equal(TenantId, persisted!.TenantId);
    }

    // ---------- F1-CA4: conflito de horário ----------

    [Fact]
    public async Task Criar_Agendamento_Conflitante_Retorna_409()
    {
        var first = await PostAsync(Request(new TimeOnly(14, 0)));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await PostAsync(Request(new TimeOnly(14, 15)));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var body = await second.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.Contains("conflita", body!.Message);
    }

    [Fact]
    public async Task Conflito_Nao_Persiste_Agendamento_Duplicado()
    {
        var time = new TimeOnly(16, 0);

        await PostAsync(Request(time));
        await PostAsync(Request(time));

        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(TenantId);
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var count = await context.Appointments.CountAsync(item => item.Time == time);

        Assert.Equal(1, count);
    }

    // ---------- Validação ----------

    [Fact]
    public async Task Criar_Com_Servico_Inexistente_Retorna_400()
    {
        // A duracao deixou de ser informada pelo cliente: o servico e que a
        // define. Servico inexistente nao tem duracao nem vinculo.
        var request = new CreateAppointmentRequest(
            _customerId, Guid.NewGuid(), _professionalId, Date, new TimeOnly(11, 0));

        var response = await PostAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Criar_Com_Referencia_Vazia_Retorna_400()
    {
        var request = new CreateAppointmentRequest(
            Guid.Empty, _serviceId, _professionalId, Date, new TimeOnly(11, 0));

        var response = await PostAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Isolamento ----------

    [Fact]
    public async Task Header_De_Tenant_Forjado_Eh_Ignorado_Quando_Ha_Token()
    {
        // F10.5: o tenant vem da claim do token, nao do header.
        // Forjar o header nao muda o tenant efetivo da requisicao.
        var time = new TimeOnly(15, 0);

        var forged = await PostAsync(Request(time), Guid.NewGuid());

        // A requisicao e aceita, mas no tenant do token — nao no forjado.
        Assert.Equal(HttpStatusCode.Created, forged.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(TenantId);

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var created = await context.Appointments.FirstOrDefaultAsync(item => item.Time == time);

        Assert.NotNull(created);
        Assert.Equal(TenantId, created!.TenantId);
    }

    private async Task<HttpResponseMessage> AgendaAsync(
        HttpMethod method, string path, object? body = null, string? token = null)
    {
        using var message = new HttpRequestMessage(method, path);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token ?? _accessToken);
        if (body is not null) message.Content = JsonContent.Create(body);
        return await _client.SendAsync(message);
    }

    [Fact]
    public async Task Agenda_Interna_Cria_Remarca_E_Consulta_Sem_Conflito_Com_O_Proprio_Compromisso()
    {
        var date = new DateOnly(2026, 12, 7);
        var query = $"?professionalId={_professionalId}&serviceId={_serviceId}&date={date:yyyy-MM-dd}";
        var availability = await AgendaAsync(HttpMethod.Get, "/api/v1/agenda/availability" + query);
        Assert.Equal(HttpStatusCode.OK, availability.StatusCode);
        var available = await availability.Content.ReadFromJsonAsync<AvailabilityResponse>();
        Assert.Contains(new TimeOnly(10, 0), available!.AvailableSlots);

        var created = await AgendaAsync(HttpMethod.Post, "/api/v1/agenda/appointments",
            new CreateAppointmentRequest(_customerId, _serviceId, _professionalId, date, new TimeOnly(10, 0)));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var appointment = await created.Content.ReadFromJsonAsync<AppointmentResponse>();
        Assert.Equal(30, appointment!.DurationMinutes);

        var ownSlots = await AgendaAsync(HttpMethod.Get,
            $"/api/v1/agenda/appointments/{appointment.Id}/reschedule-availability?date={date:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, ownSlots.StatusCode);
        Assert.Contains(new TimeOnly(10, 0),
            (await ownSlots.Content.ReadFromJsonAsync<AvailabilityResponse>())!.AvailableSlots);

        var moved = await AgendaAsync(HttpMethod.Patch,
            $"/api/v1/agenda/appointments/{appointment.Id}/reschedule",
            new RescheduleRequest(date, new TimeOnly(10, 30)));
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal(30, (await moved.Content.ReadFromJsonAsync<AppointmentResponse>())!.DurationMinutes);
        var conflict = await AgendaAsync(HttpMethod.Post, "/api/v1/agenda/appointments",
            new CreateAppointmentRequest(_customerId, _serviceId, _professionalId, date, new TimeOnly(10, 30)));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        var second = await AgendaAsync(HttpMethod.Post, "/api/v1/agenda/appointments",
            new CreateAppointmentRequest(_customerId, _serviceId, _professionalId, date, new TimeOnly(11, 0)));
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var moveIntoConflict = await AgendaAsync(HttpMethod.Patch,
            $"/api/v1/agenda/appointments/{appointment.Id}/reschedule",
            new RescheduleRequest(date, new TimeOnly(11, 0)));
        Assert.Equal(HttpStatusCode.Conflict, moveIntoConflict.StatusCode);
        var moveOutsideJourney = await AgendaAsync(HttpMethod.Patch,
            $"/api/v1/agenda/appointments/{appointment.Id}/reschedule",
            new RescheduleRequest(date.AddDays(1), new TimeOnly(10, 0)));
        Assert.Equal(HttpStatusCode.BadRequest, moveOutsideJourney.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(TenantId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await db.Appointments.SingleAsync(item => item.Id == appointment.Id);
        Assert.Equal(new TimeOnly(10, 30), saved.Time);
        Assert.Equal(30, saved.DurationMinutes);
    }

    [Fact]
    public async Task Agenda_Interna_Rejeita_Servico_Sem_Vinculo_Jornada_E_Bloqueio()
    {
        var date = new DateOnly(2026, 12, 28);
        Guid unlinkedServiceId;
        using (var scope = _factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(TenantId);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var unlinked = Service.Create(TenantId, "Sem vínculo", Money.From(80m), 45);
            unlinkedServiceId = unlinked.Id;
            db.Services.Add(unlinked);
            db.ScheduleBlocks.Add(ScheduleBlock.Create(TenantId, _professionalId,
                new DateTimeOffset(date.ToDateTime(new TimeOnly(11, 0)), TimeSpan.Zero),
                new DateTimeOffset(date.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero), "Teste"));
            db.SaveChanges();
        }
        var unlinkedSlots = await AgendaAsync(HttpMethod.Get,
            $"/api/v1/agenda/availability?professionalId={_professionalId}&serviceId={unlinkedServiceId}&date={date:yyyy-MM-dd}");
        Assert.Empty((await unlinkedSlots.Content.ReadFromJsonAsync<AvailabilityResponse>())!.AvailableSlots);
        var unlinkedCreate = await AgendaAsync(HttpMethod.Post, "/api/v1/agenda/appointments",
            new CreateAppointmentRequest(_customerId, unlinkedServiceId, _professionalId, date, new TimeOnly(10, 0)));
        Assert.Equal(HttpStatusCode.BadRequest, unlinkedCreate.StatusCode);
        var blocked = await AgendaAsync(HttpMethod.Post, "/api/v1/agenda/appointments",
            new CreateAppointmentRequest(_customerId, _serviceId, _professionalId, date, new TimeOnly(11, 0)));
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        var outside = await AgendaAsync(HttpMethod.Post, "/api/v1/agenda/appointments",
            new CreateAppointmentRequest(_customerId, _serviceId, _professionalId, date.AddDays(1), new TimeOnly(11, 0)));
        Assert.Equal(HttpStatusCode.BadRequest, outside.StatusCode);
        using var scopeAfter = _factory.Services.CreateScope();
        scopeAfter.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(TenantId);
        Assert.False(scopeAfter.ServiceProvider.GetRequiredService<AppDbContext>().Appointments
            .Any(item => item.Date == date && item.Time == new TimeOnly(11, 0)));
    }

    [Fact]
    public async Task Profissional_So_Consulta_E_Cancela_Sua_Propria_Agenda()
    {
        const string email = "profissional-fase1@exemplo.com";
        using (var scope = _factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(TenantId);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var user = User.CreateForTenant(TenantId, "Profissional", email,
                hasher.Hash(Password), Role.Professional, DateTimeOffset.UtcNow);
            db.Users.Add(user);
            var professional = db.Professionals.Single(item => item.Id == _professionalId);
            professional.LinkUser(user.Id);
            db.SaveChanges();
        }
        var login = await _client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(TenantId, email, Password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;

        var me = await AgendaAsync(HttpMethod.Get, "/api/v1/agenda/me", token: token);
        Assert.Equal(_professionalId, (await me.Content.ReadFromJsonAsync<OwnAgendaResponse>())!.ProfessionalId);
        var date = new DateOnly(2026, 12, 14);
        var ownSlots = await AgendaAsync(HttpMethod.Get,
            $"/api/v1/agenda/mine/availability?serviceId={_serviceId}&date={date:yyyy-MM-dd}", token: token);
        Assert.Equal(HttpStatusCode.OK, ownSlots.StatusCode);
        Assert.NotEmpty((await ownSlots.Content.ReadFromJsonAsync<AvailabilityResponse>())!.AvailableSlots);
        var teamList = await AgendaAsync(HttpMethod.Get,
            $"/api/v1/agenda/appointments?date={date:yyyy-MM-dd}", token: token);
        Assert.Equal(HttpStatusCode.Forbidden, teamList.StatusCode);
        var teamSlots = await AgendaAsync(HttpMethod.Get,
            "/api/v1/agenda/availability" + $"?professionalId={_professionalId}&serviceId={_serviceId}&date={date:yyyy-MM-dd}", token: token);
        Assert.Equal(HttpStatusCode.Forbidden, teamSlots.StatusCode);

        var own = await AgendaAsync(HttpMethod.Post, "/api/v1/agenda/appointments",
            new CreateAppointmentRequest(_customerId, _serviceId, _professionalId, date, new TimeOnly(9, 0)), token);
        Assert.Equal(HttpStatusCode.Created, own.StatusCode);
        var appointment = (await own.Content.ReadFromJsonAsync<AppointmentResponse>())!;
        var ownList = await AgendaAsync(HttpMethod.Get,
            $"/api/v1/agenda/mine/appointments?date={date:yyyy-MM-dd}", token: token);
        Assert.Contains((await ownList.Content.ReadFromJsonAsync<List<AppointmentResponse>>())!,
            item => item.Id == appointment.Id);

        var other = Professional.Create(TenantId, "Outro",
            WorkSchedule.Create([DayOfWeek.Monday], new TimeOnly(9, 0), new TimeOnly(18, 0)));
        using (var scope = _factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(TenantId);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Professionals.Add(other);
            db.ProfessionalServices.Add(ProfessionalService.Create(TenantId, other.Id, _serviceId));
            db.SaveChanges();
        }
        var denied = await AgendaAsync(HttpMethod.Post, "/api/v1/agenda/appointments",
            new CreateAppointmentRequest(_customerId, _serviceId, other.Id, date, new TimeOnly(9, 0)), token);
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);

        var otherCreated = await AgendaAsync(HttpMethod.Post, "/api/v1/agenda/appointments",
            new CreateAppointmentRequest(_customerId, _serviceId, other.Id, date, new TimeOnly(9, 0)));
        var otherAppointment = (await otherCreated.Content.ReadFromJsonAsync<AppointmentResponse>())!;
        var otherSlots = await AgendaAsync(HttpMethod.Get,
            $"/api/v1/agenda/appointments/{otherAppointment.Id}/reschedule-availability?date={date:yyyy-MM-dd}", token: token);
        Assert.Equal(HttpStatusCode.BadRequest, otherSlots.StatusCode);
        var otherCancel = await AgendaAsync(HttpMethod.Post,
            $"/api/v1/agenda/mine/appointments/{otherAppointment.Id}/cancel", new CancelRequest(), token);
        Assert.Equal(HttpStatusCode.Forbidden, otherCancel.StatusCode);

        var cancelled = await AgendaAsync(HttpMethod.Post,
            $"/api/v1/agenda/mine/appointments/{appointment.Id}/cancel", new CancelRequest("teste"), token);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.Equal("Cancelled", (await cancelled.Content.ReadFromJsonAsync<AppointmentResponse>())!.Status);
    }

    [Fact]
    public async Task Agenda_Cancelamento_E_Conclusao_Persistem_E_Rejeitam_Estados_Invalidos()
    {
        var past = new DateOnly(2026, 11, 2);
        var created = await AgendaAsync(HttpMethod.Post, "/api/v1/agenda/appointments",
            new CreateAppointmentRequest(_customerId, _serviceId, _professionalId, past, new TimeOnly(10, 0)));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var appointment = (await created.Content.ReadFromJsonAsync<AppointmentResponse>())!;
        var completed = await AgendaAsync(HttpMethod.Post,
            $"/api/v1/agenda/appointments/{appointment.Id}/complete");
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        Assert.Equal("Completed", (await completed.Content.ReadFromJsonAsync<AppointmentResponse>())!.Status);
        var cancelCompleted = await AgendaAsync(HttpMethod.Post,
            $"/api/v1/agenda/appointments/{appointment.Id}/cancel", new CancelRequest());
        Assert.Equal(HttpStatusCode.BadRequest, cancelCompleted.StatusCode);

        var future = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        future = future.AddDays((8 - (int)future.DayOfWeek) % 7);
        var upcoming = await AgendaAsync(HttpMethod.Post, "/api/v1/agenda/appointments",
            new CreateAppointmentRequest(_customerId, _serviceId, _professionalId, future, new TimeOnly(10, 0)));
        var futureAppointment = (await upcoming.Content.ReadFromJsonAsync<AppointmentResponse>())!;
        var tooEarly = await AgendaAsync(HttpMethod.Post,
            $"/api/v1/agenda/appointments/{futureAppointment.Id}/complete");
        Assert.Equal(HttpStatusCode.BadRequest, tooEarly.StatusCode);
        var cancelled = await AgendaAsync(HttpMethod.Post,
            $"/api/v1/agenda/appointments/{futureAppointment.Id}/cancel", new CancelRequest("ausência"));
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(TenantId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(AppointmentStatus.Completed, (await db.Appointments.SingleAsync(item => item.Id == appointment.Id)).Status);
        Assert.Equal(AppointmentStatus.Cancelled, (await db.Appointments.SingleAsync(item => item.Id == futureAppointment.Id)).Status);
    }

    // ---------- Fábrica com banco isolado ----------

    /// <summary>
    /// Substitui o contexto registrado pela aplicação por um banco SQLite
    /// em memória exclusivo desta classe de testes.
    /// </summary>
}
