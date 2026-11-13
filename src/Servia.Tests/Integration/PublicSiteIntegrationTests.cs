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

    private sealed record PublicSiteDto(string Subdomain, string BusinessName);

    private sealed record PublicServiceDto(Guid Id, string Name);
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
