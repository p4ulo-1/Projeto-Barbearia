using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Servia.Domain.Appointments;
using Servia.Domain.Catalog;
using Servia.Domain.Common;
using Servia.Domain.Sites;
using Servia.Domain.Team;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;
using Servia.Tests.Api;

namespace Servia.Tests.Integration;

/// <summary>
/// Disponibilização pública dos sites dos estabelecimentos (item 3 e 3.1).
///
/// <b>Prova:</b> o site é resolvido pelo host, só a versão publicada é
/// servida, e um estabelecimento nunca enxerga os dados do outro.
/// </summary>
public sealed class PublicSiteIntegrationTests : IClassFixture<PublicSiteApiFactory>
{
    private readonly PublicSiteApiFactory _factory;
    private readonly HttpClient _client;

    public PublicSiteIntegrationTests(PublicSiteApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureSeeded();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Site_Publicado_E_Servido_Pelo_Subdominio()
    {
        var response = await _client.GetAsync(
            $"/api/v1/public/site?host={PublicSiteApiFactory.PublishedHost}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PublicSiteDto>();
        Assert.NotNull(body);
        Assert.Equal(PublicSiteApiFactory.PublishedHost, body!.Subdomain);
    }

    [Fact]
    public async Task Site_Resolvido_Pelo_Cabecalho_Host_Real()
    {
        // Sem ?host=: o estabelecimento vem do cabecalho Host, como um
        // navegador faria ao acessar barbearia.servia.app.
        using var request = new HttpRequestMessage(
            HttpMethod.Get, "/api/v1/public/site");
        request.Headers.Host = $"{PublicSiteApiFactory.PublishedHost}.servia.app";

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PublicSiteDto>();
        Assert.NotNull(body);
        Assert.Equal(PublicSiteApiFactory.PublishedHost, body!.Subdomain);
    }

    [Fact]
    public async Task Host_Real_Sem_Site_Publicado_Retorna_404()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get, "/api/v1/public/site");
        request.Headers.Host = $"{PublicSiteApiFactory.DraftHost}.servia.app";

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Site_Nao_Publicado_Nao_E_Servido()
    {
        // O draft existe, mas nunca foi publicado.
        var response = await _client.GetAsync(
            $"/api/v1/public/site?host={PublicSiteApiFactory.DraftHost}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Host_Desconhecido_Retorna_404()
    {
        var response = await _client.GetAsync(
            "/api/v1/public/site?host=nao-existe.servia.app");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Servicos_Publicos_Sao_Do_Estabelecimento_Resolvido()
    {
        var response = await _client.GetAsync(
            $"/api/v1/public/site/services?host={PublicSiteApiFactory.PublishedHost}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var services = await response.Content.ReadFromJsonAsync<List<PublicServiceDto>>();
        Assert.NotNull(services);
        Assert.Contains(services!, service => service.Name == "Corte Publicado");

        // O servico do outro estabelecimento nunca aparece.
        Assert.DoesNotContain(services!, service => service.Name == "Corte do Outro Tenant");
    }

    // ---------- Listagem publica de profissionais ----------

    [Fact]
    public async Task Profissionais_Publicos_Trazem_Apenas_Servicos_Ativos()
    {
        var register = await _factory.RegisterPublicBookingDataAsync();

        var response = await _client.GetAsync(
            $"/api/v1/public/site/professionals?host={PublicSiteApiFactory.PublishedHost}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var professionals = await response.Content.ReadFromJsonAsync<List<PublicProfessionalDto>>();

        Assert.NotNull(professionals);

        var professional = Assert.Single(
            professionals!, item => item.Id == register.ProfessionalId);

        // O vinculo com o servico ativo aparece.
        Assert.Contains(register.ServiceId, professional.ServiceIds);
    }

    [Fact]
    public async Task Profissional_Sem_Servico_Publico_Nao_Eh_Apresentado()
    {
        // Servico ativo sem vinculo com nenhum profissional.
        var unlinkedServiceId = await _factory.RegisterUnlinkedServiceAsync();

        var response = await _client.GetAsync(
            $"/api/v1/public/site/professionals?host={PublicSiteApiFactory.PublishedHost}");

        var professionals = await response.Content.ReadFromJsonAsync<List<PublicProfessionalDto>>();

        Assert.NotNull(professionals);

        // Nenhum profissional executa o servico sem vinculo.
        Assert.DoesNotContain(
            professionals!, item => item.ServiceIds.Contains(unlinkedServiceId));

        // E todo profissional apresentado executa ao menos um servico.
        Assert.All(professionals!, item => Assert.NotEmpty(item.ServiceIds));
    }

    [Fact]
    public async Task Filtro_Por_Servico_Retorna_Apenas_Quem_O_Executa()
    {
        var register = await _factory.RegisterPublicBookingDataAsync();

        var response = await _client.GetAsync(
            $"/api/v1/public/site/professionals?host={PublicSiteApiFactory.PublishedHost}"
            + $"&serviceId={register.ServiceId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var professionals = await response.Content.ReadFromJsonAsync<List<PublicProfessionalDto>>();

        Assert.NotNull(professionals);
        Assert.NotEmpty(professionals!);
        Assert.All(professionals!, item => Assert.Contains(register.ServiceId, item.ServiceIds));
    }

    [Fact]
    public async Task Filtro_Por_Servico_Sem_Vinculo_Retorna_Lista_Vazia()
    {
        var unlinkedServiceId = await _factory.RegisterUnlinkedServiceAsync();

        var response = await _client.GetAsync(
            $"/api/v1/public/site/professionals?host={PublicSiteApiFactory.PublishedHost}"
            + $"&serviceId={unlinkedServiceId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var professionals = await response.Content.ReadFromJsonAsync<List<PublicProfessionalDto>>();

        Assert.NotNull(professionals);
        Assert.Empty(professionals!);
    }

    // ---------- Validacao da disponibilidade publica ----------

    [Fact]
    public async Task Disponibilidade_Publica_Sem_Servico_Retorna_400()
    {
        var register = await _factory.RegisterPublicBookingDataAsync();

        var response = await _client.GetAsync(
            $"/api/v1/public/site/availability?host={PublicSiteApiFactory.PublishedHost}"
            + $"&professionalId={register.ProfessionalId}&date={register.Date:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Disponibilidade_Publica_Sem_Profissional_Retorna_400()
    {
        var register = await _factory.RegisterPublicBookingDataAsync();

        var response = await _client.GetAsync(
            $"/api/v1/public/site/availability?host={PublicSiteApiFactory.PublishedHost}"
            + $"&serviceId={register.ServiceId}&date={register.Date:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Disponibilidade_Publica_Com_Servico_Inexistente_Retorna_404()
    {
        var register = await _factory.RegisterPublicBookingDataAsync();

        var response = await _client.GetAsync(
            $"/api/v1/public/site/availability?host={PublicSiteApiFactory.PublishedHost}"
            + $"&professionalId={register.ProfessionalId}&serviceId={Guid.NewGuid()}"
            + $"&date={register.Date:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Disponibilidade_Publica_Valida_Sem_Horarios_Retorna_200_Vazio()
    {
        var register = await _factory.RegisterPublicBookingDataAsync();

        // Data distante, exclusiva deste teste: o bloqueio não vaza para os
        // demais cenários, que usam a data padrão do profissional.
        var isolatedDate = register.Date.AddDays(30);

        // Bloqueia o dia inteiro: pedido valido, agenda realmente sem horario.
        await _factory.RegisterBlockAsync(
            isolatedDate, new TimeOnly(0, 0), new TimeOnly(23, 59));

        var response = await _client.GetAsync(
            $"/api/v1/public/site/availability?host={PublicSiteApiFactory.PublishedHost}"
            + $"&professionalId={register.ProfessionalId}&serviceId={register.ServiceId}"
            + $"&date={isolatedDate:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PublicAvailabilityDto>();

        Assert.NotNull(body);
        Assert.Empty(body!.Slots);
    }

    [Fact]
    public async Task Agendamento_Publico_E_Criado_E_Respeita_Disponibilidade()
    {
        var register = await _factory.RegisterPublicBookingDataAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/public/site/appointments?host={PublicSiteApiFactory.PublishedHost}",
            new
            {
                name = "Cliente Público",
                phone = "11912345678",
                email = "cliente@exemplo.com",
                serviceId = register.ServiceId,
                professionalId = register.ProfessionalId,
                date = register.Date.ToString("yyyy-MM-dd"),
                time = "09:00:00"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Agendamento_Conflitante_E_Rejeitado()
    {
        var register = await _factory.RegisterPublicBookingDataAsync();

        var payload = new
        {
            name = "Cliente Público",
            phone = "11912345679",
            email = "cliente2@exemplo.com",
            serviceId = register.ServiceId,
            professionalId = register.ProfessionalId,
            date = register.Date.ToString("yyyy-MM-dd"),
            time = "10:00:00"
        };

        var first = await _client.PostAsJsonAsync(
            $"/api/v1/public/site/appointments?host={PublicSiteApiFactory.PublishedHost}", payload);

        var second = await _client.PostAsJsonAsync(
            $"/api/v1/public/site/appointments?host={PublicSiteApiFactory.PublishedHost}", payload);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Site_De_Outro_Tenant_Nao_Vaza_Servicos()
    {
        // O host do segundo estabelecimento nao tem site publicado; mesmo que
        // tivesse, o filtro de tenant restringe serviços ao estabelecimento.
        var response = await _client.GetAsync(
            $"/api/v1/public/site/services?host={PublicSiteApiFactory.DraftHost}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Agendamento_Conflitante_Nao_Deixa_Cliente_Orfao()
    {
        var register = await _factory.RegisterPublicBookingDataAsync();

        // Primeiro agendamento ocupa o horario.
        var basePayload = new
        {
            name = "Cliente Original",
            phone = "11922220000",
            email = (string?)null,
            serviceId = register.ServiceId,
            professionalId = register.ProfessionalId,
            date = register.Date.ToString("yyyy-MM-dd"),
            time = "11:00:00"
        };

        var first = await _client.PostAsJsonAsync(
            $"/api/v1/public/site/appointments?host={PublicSiteApiFactory.PublishedHost}", basePayload);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Segundo pedido, MESMO horario, telefone novo: conflita.
        var conflictResponse = await _client.PostAsJsonAsync(
            $"/api/v1/public/site/appointments?host={PublicSiteApiFactory.PublishedHost}",
            new
            {
                name = "Cliente Novo",
                phone = "11922221111",
                email = (string?)null,
                serviceId = register.ServiceId,
                professionalId = register.ProfessionalId,
                date = register.Date.ToString("yyyy-MM-dd"),
                time = "11:00:00"
            });

        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);

        // C6: o cliente do pedido conflitante nao foi persistido.
        Assert.False(await _factory.CustomerExistsAsync("11922221111"));
    }

    [Fact]
    public async Task Agendamento_Fora_Do_Expediente_E_Rejeitado()
    {
        var register = await _factory.RegisterPublicBookingDataAsync();

        // Expediente 8-18; 20:00 está fora.
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/public/site/appointments?host={PublicSiteApiFactory.PublishedHost}",
            new
            {
                name = "Cliente Fora",
                phone = "11912340000",
                email = (string?)null,
                serviceId = register.ServiceId,
                professionalId = register.ProfessionalId,
                date = register.Date.ToString("yyyy-MM-dd"),
                time = "20:00:00"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Agendamento_Publico_Rejeita_Profissional_Sem_Vinculo()
    {
        // O canal público aplica a mesma regra do interno: um profissional
        // que não executa o serviço não pode receber o agendamento.
        var register = await _factory.RegisterPublicBookingDataAsync();
        var unlinkedServiceId = await _factory.RegisterUnlinkedServiceAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/public/site/appointments?host={PublicSiteApiFactory.PublishedHost}",
            new
            {
                name = "Cliente Sem Vinculo",
                phone = "11912341111",
                email = (string?)null,
                serviceId = unlinkedServiceId,
                professionalId = register.ProfessionalId,
                date = register.Date.ToString("yyyy-MM-dd"),
                time = "10:00:00"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Agendamento_Sobre_Bloqueio_E_Rejeitado()
    {
        var register = await _factory.RegisterPublicBookingDataAsync();

        // Bloqueia 14:00-15:00 na data do agendamento.
        await _factory.RegisterBlockAsync(register.Date, new TimeOnly(14, 0), new TimeOnly(15, 0));

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/public/site/appointments?host={PublicSiteApiFactory.PublishedHost}",
            new
            {
                name = "Cliente Bloqueio",
                phone = "11912340001",
                email = (string?)null,
                serviceId = register.ServiceId,
                professionalId = register.ProfessionalId,
                date = register.Date.ToString("yyyy-MM-dd"),
                time = "14:00:00"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Fluxo_Anonimo_Pelo_Host_Persiste_E_Aparece_Na_Agenda_Interna()
    {
        var register = await _factory.RegisterPublicBookingDataAsync();
        using var visitor = _factory.CreateClient();
        visitor.DefaultRequestHeaders.Host = $"{PublicSiteApiFactory.PublishedHost}.servia.app";
        var site = await visitor.GetAsync("/api/v1/public/site");
        Assert.Equal(HttpStatusCode.OK, site.StatusCode);
        var services = await visitor.GetFromJsonAsync<List<PublicServiceDto>>("/api/v1/public/site/services");
        Assert.Contains(services!, service => service.Id == register.ServiceId);
        var professionals = await visitor.GetFromJsonAsync<List<PublicProfessionalDto>>(
            $"/api/v1/public/site/professionals?serviceId={register.ServiceId}");
        Assert.Contains(professionals!, person => person.Id == register.ProfessionalId);
        var date = register.Date.AddDays(30);
        var availability = await visitor.GetFromJsonAsync<PublicAvailabilityDto>(
            $"/api/v1/public/site/availability?serviceId={register.ServiceId}&professionalId={register.ProfessionalId}&date={date:yyyy-MM-dd}");
        var time = availability!.Slots.First();
        var request = new { name = "Cliente Fluxo", phone = "11933334444", email = (string?)null,
            serviceId = register.ServiceId, professionalId = register.ProfessionalId, date, time };
        var created = await visitor.PostAsJsonAsync("/api/v1/public/site/appointments", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var appointment = await created.Content.ReadFromJsonAsync<Servia.Api.Controllers.PublicAppointmentResponse>();
        var conflict = await visitor.PostAsJsonAsync("/api/v1/public/site/appointments", request);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        Guid tenantId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            tenantId = (await db.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Slug == PublicSiteApiFactory.PublishedHost)).Id;
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
            var saved = await db.Appointments.SingleAsync(a => a.Id == appointment!.Id);
            Assert.Equal(register.ServiceId, saved.ServiceId);
            Assert.Equal(register.ProfessionalId, saved.ProfessionalId);
            Assert.Equal(date, saved.Date);
            Assert.Equal(time, saved.Time);
            Assert.Equal(30, saved.DurationMinutes);
            var hasher = scope.ServiceProvider.GetRequiredService<Servia.Domain.Identity.IPasswordHasher>();
            db.Users.Add(Servia.Domain.Identity.User.CreateForTenant(tenantId, "Owner fluxo", "fluxo@teste.local",
                hasher.Hash("SenhaSegura123"), Servia.Domain.Identity.Role.Owner, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }
        var login = await _client.PostAsJsonAsync("/api/auth/sign-in",
            new Servia.Api.Contracts.SignInRequest(tenantId, "fluxo@teste.local", "SenhaSegura123"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = await login.Content.ReadFromJsonAsync<Servia.Api.Contracts.AuthResponse>();
        using var agendaRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/agenda/appointments?date={date:yyyy-MM-dd}");
        agendaRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        var agenda = await _client.SendAsync(agendaRequest);
        Assert.Equal(HttpStatusCode.OK, agenda.StatusCode);
        var entries = await agenda.Content.ReadFromJsonAsync<List<Servia.Api.Contracts.AppointmentResponse>>();
        Assert.Contains(entries!, entry => entry.Id == appointment!.Id);

        // Os dois canais consultam a mesma elegibilidade após a reserva.
        var publicAfter = await visitor.GetFromJsonAsync<PublicAvailabilityDto>(
            $"/api/v1/public/site/availability?serviceId={register.ServiceId}&professionalId={register.ProfessionalId}&date={date:yyyy-MM-dd}");
        using var internalRequest = new HttpRequestMessage(HttpMethod.Get,
            $"/api/v1/agenda/availability?serviceId={register.ServiceId}&professionalId={register.ProfessionalId}&date={date:yyyy-MM-dd}");
        internalRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var internalAfter = await _client.SendAsync(internalRequest);
        Assert.Equal(HttpStatusCode.OK, internalAfter.StatusCode);
        var internalSlots = await internalAfter.Content.ReadFromJsonAsync<Servia.Api.Controllers.AvailabilityResponse>();
        Assert.Equal(publicAfter!.Slots, internalSlots!.AvailableSlots);
        Assert.DoesNotContain(time, internalSlots.AvailableSlots);
    }

    private sealed record PublicSiteDto(string Subdomain, string BusinessName);

    private sealed record PublicServiceDto(Guid Id, string Name);

    private sealed record PublicProfessionalDto(Guid Id, string Name, List<Guid> ServiceIds);

    private sealed record PublicAvailabilityDto(Guid ProfessionalId, DateOnly Date, List<TimeOnly> Slots);
}

/// <summary>
/// Prepara dois estabelecimentos: um com site publicado e um apenas em rascunho.
/// </summary>
public class PublicSiteApiFactory : IsolatedApiFactory
{
    public const string PublishedHost = "publicado-teste";

    public const string DraftHost = "rascunho-teste";

    private bool _seeded;

    public sealed record BookingData(Guid ServiceId, Guid ProfessionalId, DateOnly Date);

    public void EnsureSeeded()
    {
        if (_seeded)
        {
            return;
        }

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<Servia.Domain.Identity.IPasswordHasher>();

        if (context.Sites.IgnoreQueryFilters().Any())
        {
            _seeded = true;
            return;
        }

        var now = DateTimeOffset.UtcNow;

        var publishedTenant = Tenant.Create(
            PublishedHost, "Publicado", SegmentType.Barbershop, "11111111000111", now);
        var draftTenant = Tenant.Create(
            DraftHost, "Rascunho", SegmentType.Barbershop, "22222222000122", now);

        context.Tenants.AddRange(publishedTenant, draftTenant);

        // Servicos
        var servicePublished = Service.Create(
            publishedTenant.Id, "Corte Publicado", Money.From(50m), 30);
        var serviceOther = Service.Create(
            draftTenant.Id, "Corte do Outro Tenant", Money.From(40m), 30);

        context.Services.AddRange(servicePublished, serviceOther);

        // Profissional com jornada em todos os dias.
        var schedule = WorkSchedule.Create(
            Enum.GetValues<DayOfWeek>(), new TimeOnly(8, 0), new TimeOnly(18, 0), 30);

        var professional = Professional.Create(
            publishedTenant.Id, "Profissional Publicado", schedule);

        context.Professionals.Add(professional);

        context.ProfessionalServices.Add(
            ProfessionalService.Create(publishedTenant.Id, professional.Id, servicePublished.Id));

        // Site publicado com snapshot.
        var site = Site.Create(publishedTenant.Id, PublishedHost);
        var page = site.Pages.First();
        var hero = page.AddSection(SiteComponentType.Hero);
        hero.SetProperty("title", "Bem-vindo");

        site.Publish(Guid.NewGuid(), now);

        context.Sites.Add(site);

        // Site em rascunho: nunca publicado.
        var draftSite = Site.Create(draftTenant.Id, DraftHost);
        context.Sites.Add(draftSite);

        context.SaveChanges();

        _seeded = true;
    }

    /// <summary>
    /// Cria um serviço ativo no estabelecimento publicado, sem vínculo com
    /// nenhum profissional. Serve para provar que o canal público recusa
    /// agendamento de serviço que o profissional não executa.
    /// </summary>
    public async Task<Guid> RegisterUnlinkedServiceAsync()
    {
        EnsureSeeded();

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenant = context.Tenants.IgnoreQueryFilters()
            .First(item => item.Slug == PublishedHost);

        var service = Service.Create(
            tenant.Id, "Servico Sem Vinculo", Money.From(30m), 30);

        context.Services.Add(service);
        await context.SaveChangesAsync();

        return service.Id;
    }

    /// <summary>Cria serviço e profissional prontos para o agendamento público.</summary>
    public async Task<BookingData> RegisterPublicBookingDataAsync()
    {
        EnsureSeeded();

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenant = context.Tenants.IgnoreQueryFilters()
            .First(item => item.Slug == PublishedHost);

        var service = context.Services.IgnoreQueryFilters()
            .First(item => item.TenantId == tenant.Id && item.Name == "Corte Publicado");

        var professional = context.Professionals.IgnoreQueryFilters()
            .First(item => item.TenantId == tenant.Id);

        // Uma data futura em que o profissional trabalha.
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        return await Task.FromResult(
            new BookingData(service.Id, professional.Id, date));
    }

    /// <summary>Indica se existe cliente com o telefone no estabelecimento publicado.</summary>
    public async Task<bool> CustomerExistsAsync(string phone)
    {
        EnsureSeeded();

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenant = context.Tenants.IgnoreQueryFilters()
            .First(item => item.Slug == PublishedHost);

        return await context.Customers.IgnoreQueryFilters()
            .AnyAsync(customer => customer.TenantId == tenant.Id && customer.Phone == phone);
    }

    /// <summary>Cria um bloqueio de agenda do profissional publicado.</summary>
    public async Task RegisterBlockAsync(DateOnly date, TimeOnly start, TimeOnly end)
    {
        EnsureSeeded();

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenant = context.Tenants.IgnoreQueryFilters()
            .First(item => item.Slug == PublishedHost);

        var professional = context.Professionals.IgnoreQueryFilters()
            .First(item => item.TenantId == tenant.Id);

        context.ScheduleBlocks.Add(ScheduleBlock.Create(
            tenant.Id,
            professional.Id,
            new DateTimeOffset(date.ToDateTime(start), TimeSpan.Zero),
            new DateTimeOffset(date.ToDateTime(end), TimeSpan.Zero),
            "Bloqueio de teste"));

        await context.SaveChangesAsync();
    }
}
