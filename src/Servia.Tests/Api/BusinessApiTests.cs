using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Servia.Api.Contracts;
using Servia.Api.Controllers;
using Servia.Domain.Appointments;
using Servia.Domain.Catalog;
using Servia.Domain.Common;
using Servia.Domain.Identity;
using Servia.Domain.Team;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Identity;
using Servia.Infrastructure.Persistence;

namespace Servia.Tests.Api;

/// <summary>
/// F13.1–F13.11 — API de negócio.
///
/// Exercita os endpoints reais: catálogo, equipe, clientes, agenda,
/// financeiro, módulos e paginação. Verifica status, contrato e isolamento.
/// </summary>
public class BusinessApiTests : IClassFixture<IsolatedApiFactory>
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private const string Password = "SenhaSegura123";

    private readonly IsolatedApiFactory _factory;
    private readonly HttpClient _client;
    private readonly string _token;

    public BusinessApiTests(IsolatedApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();

        SeedOwner();
        _token = SignIn();
    }

    private void SeedOwner()
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(TenantId);

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        if (context.Users.Any())
        {
            return;
        }

        context.Users.Add(User.CreateForTenant(
            TenantId, "Proprietario", "owner@exemplo.com",
            hasher.Hash(Password), Role.Owner, DateTimeOffset.UtcNow));

        context.SaveChanges();
    }

    private string SignIn()
    {
        var response = _client.PostAsJsonAsync("/api/auth/sign-in", new SignInRequest(
            TenantId, "owner@exemplo.com", Password)).GetAwaiter().GetResult();

        var body = response.Content.ReadFromJsonAsync<AuthResponse>().GetAwaiter().GetResult();

        return body!.AccessToken;
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string url,
        object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        return await _client.SendAsync(request);
    }

    // ---------- F13.1: catalogo ----------

    [Fact]
    public async Task Catalogo_Cria_E_Lista_Servico()
    {
        var created = await SendAsync(HttpMethod.Post, "/api/v1/catalog/services",
            new CreateServiceRequest("Corte", 50m, 30));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var listed = await SendAsync(HttpMethod.Get, "/api/v1/catalog/services?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);

        var page = await listed.Content.ReadFromJsonAsync<PagedResponse<ServiceResponse>>();

        Assert.NotNull(page);
        Assert.Contains(page!.Items, item => item.Name == "Corte");
    }

    [Fact]
    public async Task Catalogo_Altera_Preco()
    {
        var created = await SendAsync(HttpMethod.Post, "/api/v1/catalog/services",
            new CreateServiceRequest("Barba", 30m, 20));

        var service = await created.Content.ReadFromJsonAsync<ServiceResponse>();

        var changed = await SendAsync(HttpMethod.Patch,
            $"/api/v1/catalog/services/{service!.Id}/price", new ChangePriceRequest(45m));

        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);

        var updated = await changed.Content.ReadFromJsonAsync<ServiceResponse>();
        Assert.Equal(45m, updated!.Price);
    }

    [Fact]
    public async Task Catalogo_Consulta_E_Edita_Servico_Gratuito_Pelo_Contrato()
    {
        var categoryResponse = await SendAsync(HttpMethod.Post, "/api/v1/catalog/categories",
            new CreateCategoryRequest("Categoria para edição"));
        Assert.Equal(HttpStatusCode.Created, categoryResponse.StatusCode);
        var category = (await categoryResponse.Content.ReadFromJsonAsync<CategoryResponse>())!;

        var created = await SendAsync(HttpMethod.Post, "/api/v1/catalog/services",
            new CreateServiceRequest("Consulta gratuita", 0m, 30));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var route = created.Headers.Location!.OriginalString;

        var edited = await SendAsync(HttpMethod.Put, route,
            new UpdateServiceRequest("Consulta editada", 45, "Descrição", category.Id, true));
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var detail = await SendAsync(HttpMethod.Get, route);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var updated = (await detail.Content.ReadFromJsonAsync<ServiceResponse>())!;
        Assert.Equal(new ServiceResponse(updated.Id, "Consulta editada", "Descrição", 0m, 45, category.Id, true, true), updated);

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Patch, route + "/price",
            new ChangePriceRequest(40m))).StatusCode);
        var free = await SendAsync(HttpMethod.Patch, route + "/price", new ChangePriceRequest(0m));
        Assert.Equal(HttpStatusCode.OK, free.StatusCode);
        Assert.Equal(0m, (await free.Content.ReadFromJsonAsync<ServiceResponse>())!.Price);
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(HttpMethod.Patch, route + "/price",
            new { })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(HttpMethod.Put, route,
            new UpdateServiceRequest("Duração inválida", 0))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(HttpMethod.Put, route,
            new UpdateServiceRequest(" ", 30))).StatusCode);
        var unchanged = await SendAsync(HttpMethod.Get, route);
        Assert.Equal(updated, await unchanged.Content.ReadFromJsonAsync<ServiceResponse>());
    }

    [Theory]
    [InlineData(-1, 30)]
    [InlineData(0, 0)]
    public async Task Catalogo_Rejeita_Cadastro_Com_Preco_Ou_Duracao_Invalidos(int price, int duration)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/v1/catalog/services",
            new CreateServiceRequest("Inválido", price, duration));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Catalogo_Rejeita_Preco_Omitido_E_Recursos_De_Outro_Tenant()
    {
        var foreignTenant = Guid.NewGuid();
        var foreignService = Service.Create(foreignTenant, "Serviço externo", Money.Zero(), 30);
        var foreignCategory = ServiceCategory.Create(foreignTenant, "Categoria externa");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AddRange(foreignService, foreignCategory);
        await db.SaveChangesAsync();

        const string services = "/api/v1/catalog/services";
        var foreignRoute = $"{services}/{foreignService.Id}";
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(HttpMethod.Post, services,
            new { name = "Sem preço", durationMinutes = 30 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Post, services,
            new CreateServiceRequest("Categoria externa", 0m, 30, CategoryId: foreignCategory.Id))).StatusCode);
        var own = await SendAsync(HttpMethod.Post, services, new CreateServiceRequest("Serviço local", 0m, 30));
        Assert.Equal(HttpStatusCode.Created, own.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Put, own.Headers.Location!.OriginalString,
            new UpdateServiceRequest("Categoria externa", 30, CategoryId: foreignCategory.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Get, foreignRoute)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Put, foreignRoute,
            new UpdateServiceRequest("Nome indevido", 60))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Post, foreignRoute + "/activate")).StatusCode);
    }

    [Fact]
    public async Task Catalogo_Desativa_Servico_E_Responde_A_Referencias_Inexistentes()
    {
        var created = await SendAsync(HttpMethod.Post, "/api/v1/catalog/services",
            new CreateServiceRequest("Serviço para desativar", 30m, 20));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var service = (await created.Content.ReadFromJsonAsync<ServiceResponse>())!;

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Post,
            $"/api/v1/catalog/services/{service.Id}/deactivate")).StatusCode);
        var listed = await SendAsync(HttpMethod.Get, "/api/v1/catalog/services?page=1&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        var page = (await listed.Content.ReadFromJsonAsync<PagedResponse<ServiceResponse>>())!;
        Assert.Contains(page.Items, item => item.Id == service.Id && !item.IsActive);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Post,
            $"/api/v1/catalog/services/{Guid.NewGuid()}/deactivate")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Patch,
            $"/api/v1/catalog/services/{Guid.NewGuid()}/price", new ChangePriceRequest(45m))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(HttpMethod.Patch,
            $"/api/v1/catalog/services/{service.Id}/price", new ChangePriceRequest(-1m))).StatusCode);
    }

    [Fact]
    public async Task Catalogo_Cria_Categoria()
    {
        var response = await SendAsync(HttpMethod.Post, "/api/v1/catalog/categories",
            new CreateCategoryRequest("Cortes", 1));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Equipe_Consulta_E_Edita_Cadastro_Preservando_Jornada_E_Foto()
    {
        var created = await SendAsync(HttpMethod.Post, "/api/v1/team/professionals",
            new CreateProfessionalRequest("Nome original", [DayOfWeek.Tuesday],
                new TimeOnly(9, 0), new TimeOnly(17, 0), 20, "Título original", "Biografia original",
                IsServiceProvider: false));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var route = created.Headers.Location!.OriginalString;
        var professional = (await created.Content.ReadFromJsonAsync<ProfessionalResponse>())!;
        const string photo = "/assets/foto-existente.png";
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(TenantId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.Professionals.SingleAsync(item => item.Id == professional.Id);
        stored.UpdateProfile(stored.Name, stored.Title, stored.Bio, photo);
        await db.SaveChangesAsync();

        var initial = await SendAsync(HttpMethod.Get, route);
        Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
        Assert.Equal("Biografia original", (await initial.Content.ReadFromJsonAsync<ProfessionalDetailResponse>())!.Bio);

        var edited = await SendAsync(HttpMethod.Put, route,
            new UpdateProfessionalRequest("Nome editado", "Especialista", "Biografia editada"));
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var expected = new ProfessionalDetailResponse(
            professional.Id, "Nome editado", "Especialista", "Biografia editada", true, false);
        Assert.Equal(expected, await edited.Content.ReadFromJsonAsync<ProfessionalDetailResponse>());
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(HttpMethod.Put, route,
            new UpdateProfessionalRequest(" "))).StatusCode);
        var confirmed = await SendAsync(HttpMethod.Get, route);
        Assert.Equal(expected, await confirmed.Content.ReadFromJsonAsync<ProfessionalDetailResponse>());

        var persisted = await db.Professionals.AsNoTracking().SingleAsync(item => item.Id == professional.Id);
        Assert.Equal(photo, persisted.PhotoUrl);
        Assert.Equal(DayOfWeek.Tuesday, Assert.Single(persisted.Schedule.WorkDays));
        Assert.Equal(new TimeOnly(9, 0), persisted.Schedule.StartTime);
        Assert.Equal(new TimeOnly(17, 0), persisted.Schedule.EndTime);
        Assert.Equal(20, persisted.Schedule.SlotIntervalMinutes);

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Post, route + "/deactivate")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Post, route + "/deactivate")).StatusCode);
        const string listRoute = "/api/v1/team/professionals?page=1&pageSize=100";
        var activeOnly = await SendAsync(HttpMethod.Get, listRoute);
        var all = await SendAsync(HttpMethod.Get, listRoute + "&includeInactive=true");
        Assert.Equal(HttpStatusCode.OK, activeOnly.StatusCode);
        Assert.Equal(HttpStatusCode.OK, all.StatusCode);
        Assert.DoesNotContain((await activeOnly.Content.ReadFromJsonAsync<PagedResponse<ProfessionalResponse>>())!.Items,
            item => item.Id == professional.Id);
        Assert.Contains((await all.Content.ReadFromJsonAsync<PagedResponse<ProfessionalResponse>>())!.Items,
            item => item.Id == professional.Id && !item.IsActive);
    }

    [Fact]
    public async Task Equipe_Rejeita_Edicao_Ativacao_E_Vinculos_Entre_Tenants()
    {
        var foreignTenant = Guid.NewGuid();
        var foreignProfessional = Professional.Create(foreignTenant, "Pessoa externa",
            WorkSchedule.Create([DayOfWeek.Monday], new TimeOnly(9, 0), new TimeOnly(18, 0)));
        var foreignService = Service.Create(foreignTenant, "Serviço externo", Money.Zero(), 30);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AddRange(foreignProfessional, foreignService);
        await db.SaveChangesAsync();

        const string team = "/api/v1/team/professionals";
        var foreignRoute = $"{team}/{foreignProfessional.Id}";
        var owned = await SendAsync(HttpMethod.Post, team,
            new CreateProfessionalRequest("Pessoa local", [DayOfWeek.Monday], new TimeOnly(9, 0), new TimeOnly(18, 0)));
        Assert.Equal(HttpStatusCode.Created, owned.StatusCode);
        var foreignLink = $"{owned.Headers.Location!.OriginalString}/services/{foreignService.Id}";
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Get, foreignRoute)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Put, foreignRoute,
            new UpdateProfessionalRequest("Nome indevido"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Post, foreignRoute + "/activate")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Post, foreignRoute + "/deactivate")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Post, foreignLink)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Delete, foreignLink)).StatusCode);
    }

    [Fact]
    public async Task Jornada_Valida_Dias_Intervalos_E_Tenant()
    {
        var created = await SendAsync(HttpMethod.Post, "/api/v1/team/professionals",
            new CreateProfessionalRequest("Jornada", [DayOfWeek.Monday], new TimeOnly(8, 0), new TimeOnly(17, 0)));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var route = created.Headers.Location!.OriginalString + "/schedule";
        var updated = new UpdateProfessionalScheduleRequest(
            [DayOfWeek.Wednesday], new TimeOnly(9, 0), new TimeOnly(13, 0), 15);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Put, route, updated)).StatusCode);

        foreach (var invalid in new[]
        {
            updated with { WorkDays = [(DayOfWeek)8] },
            updated with { EndTime = updated.StartTime },
            updated with { SlotIntervalMinutes = 4 },
            updated with { SlotIntervalMinutes = 121 }
        })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(HttpMethod.Put, route, invalid)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(HttpMethod.Put, route,
            new { workDays = new[] { 1 }, endTime = "18:00:00" })).StatusCode);

        var response = await SendAsync(HttpMethod.Get, route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var schedule = (await response.Content.ReadFromJsonAsync<ProfessionalScheduleResponse>())!;
        Assert.Equal(updated.WorkDays.ToArray(), schedule.WorkDays.ToArray());
        Assert.Equal(updated.StartTime, schedule.StartTime);
        Assert.Equal(updated.EndTime, schedule.EndTime);
        Assert.Equal(updated.SlotIntervalMinutes, schedule.SlotIntervalMinutes);

        var foreign = Professional.Create(Guid.NewGuid(), "Jornada externa",
            WorkSchedule.Create([DayOfWeek.Monday], new TimeOnly(8, 0), new TimeOnly(17, 0)));
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Professionals.Add(foreign);
        await db.SaveChangesAsync();
        var foreignRoute = $"/api/v1/team/professionals/{foreign.Id}/schedule";
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Get, foreignRoute)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Put, foreignRoute, updated)).StatusCode);
    }

    [Fact]
    public async Task Bloqueios_Cadastra_Consulta_Remove_E_Restringe_Tenant_E_Profissional()
    {
        var created = await SendAsync(HttpMethod.Post, "/api/v1/team/professionals",
            new CreateProfessionalRequest("Bloqueios", [DayOfWeek.Monday], new TimeOnly(8, 0), new TimeOnly(17, 0)));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var route = created.Headers.Location!.OriginalString + "/blocks";
        var start = new DateTimeOffset(2026, 12, 7, 9, 0, 0, TimeSpan.FromHours(-3));
        var request = new CreateBlockRequest(start, start.AddHours(1), "  Compromisso  ");
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(HttpMethod.Post, route,
            request with { End = start })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(HttpMethod.Post, route,
            new { end = request.End })).StatusCode);

        var blockResponse = await SendAsync(HttpMethod.Post, route, request);
        Assert.Equal(HttpStatusCode.Created, blockResponse.StatusCode);
        var detailRoute = blockResponse.Headers.Location!.OriginalString;
        var found = await SendAsync(HttpMethod.Get, detailRoute);
        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        var block = (await found.Content.ReadFromJsonAsync<BlockResponse>())!;
        Assert.Equal(start, block.Start);
        Assert.Equal(TimeSpan.Zero, block.Start.Offset);
        Assert.Equal(request.End, block.End);
        Assert.Equal("Compromisso", block.Reason);

        var foreign = Professional.Create(Guid.NewGuid(), "Bloqueio externo",
            WorkSchedule.Create([DayOfWeek.Monday], new TimeOnly(8, 0), new TimeOnly(17, 0)));
        var foreignBlock = ScheduleBlock.Create(foreign.TenantId, foreign.Id, start, start.AddHours(1));
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AddRange(foreign, foreignBlock);
        await db.SaveChangesAsync();

        var foreignRoute = $"/api/v1/team/professionals/{foreign.Id}/blocks";
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Post, foreignRoute, request)).StatusCode);
        var foreignList = await SendAsync(HttpMethod.Get, foreignRoute);
        Assert.Empty((await foreignList.Content.ReadFromJsonAsync<BlockResponse[]>())!);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Get, $"{foreignRoute}/{foreignBlock.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Delete, $"{foreignRoute}/{foreignBlock.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Delete, $"{foreignRoute}/{block.Id}")).StatusCode);

        var listed = await SendAsync(HttpMethod.Get, route);
        Assert.Equal(block, Assert.Single((await listed.Content.ReadFromJsonAsync<BlockResponse[]>())!));
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Delete, detailRoute)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(HttpMethod.Get, detailRoute)).StatusCode);
        listed = await SendAsync(HttpMethod.Get, route);
        Assert.Empty((await listed.Content.ReadFromJsonAsync<BlockResponse[]>())!);
    }

    // ---------- F13.2: equipe ----------

    [Fact]
    public async Task Equipe_Cria_Profissional_Com_Jornada()
    {
        var response = await SendAsync(HttpMethod.Post, "/api/v1/team/professionals",
            new CreateProfessionalRequest(
                "Ana", [DayOfWeek.Monday, DayOfWeek.Tuesday],
                new TimeOnly(9, 0), new TimeOnly(18, 0)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Equipe_Rejeita_Jornada_Invalida()
    {
        var response = await SendAsync(HttpMethod.Post, "/api/v1/team/professionals",
            new CreateProfessionalRequest(
                "Ana", [DayOfWeek.Monday],
                new TimeOnly(18, 0), new TimeOnly(9, 0)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- F13.3: clientes ----------

    [Fact]
    public async Task Clientes_Cadastra_E_Busca()
    {
        var created = await SendAsync(HttpMethod.Post, "/api/v1/customers",
            new CreateCustomerRequest("João", "21999998888"));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var customer = await created.Content.ReadFromJsonAsync<CustomerResponse>();

        var fetched = await SendAsync(HttpMethod.Get, $"/api/v1/customers/{customer!.Id}");

        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
    }

    [Fact]
    public async Task Clientes_Anonimiza_Por_Lgpd()
    {
        var created = await SendAsync(HttpMethod.Post, "/api/v1/customers",
            new CreateCustomerRequest("Maria", "21999997777", "maria@exemplo.com"));

        var customer = await created.Content.ReadFromJsonAsync<CustomerResponse>();

        var anonymized = await SendAsync(HttpMethod.Post,
            $"/api/v1/customers/{customer!.Id}/anonymize");

        Assert.Equal(HttpStatusCode.NoContent, anonymized.StatusCode);

        var fetched = await SendAsync(HttpMethod.Get, $"/api/v1/customers/{customer.Id}");
        var body = await fetched.Content.ReadFromJsonAsync<CustomerResponse>();

        Assert.Equal("Cliente anonimizado", body!.Name);
        Assert.Null(body.Email);
    }

    // ---------- F13.4: agenda ----------

    [Fact]
    public async Task Agenda_Consulta_Disponibilidade()
    {
        var (professional, service) = await CreateLinkedProfessionalAsync();

        var response = await SendAsync(HttpMethod.Get,
            $"/api/v1/agenda/availability?professionalId={professional}&serviceId={service}&date=2026-11-16");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var availability = await response.Content.ReadFromJsonAsync<AvailabilityResponse>();

        Assert.NotNull(availability);
        Assert.NotEmpty(availability!.AvailableSlots);
    }

    [Fact]
    public async Task Agenda_Nao_Oferece_Horario_Em_Dia_Nao_Trabalhado()
    {
        var (professional, service) = await CreateLinkedProfessionalAsync();

        // 2026-11-15 e domingo; a jornada e segunda e terca.
        var response = await SendAsync(HttpMethod.Get,
            $"/api/v1/agenda/availability?professionalId={professional}&serviceId={service}&date=2026-11-15");

        var availability = await response.Content.ReadFromJsonAsync<AvailabilityResponse>();

        Assert.Empty(availability!.AvailableSlots);
    }

    [Fact]
    public async Task Agenda_Nao_Oferece_Horario_Para_Profissional_Sem_Vinculo()
    {
        // Profissional sem vínculo com o serviço: não há horário a oferecer.
        var professional = await CreateProfessionalAsync();

        var serviceResponse = await SendAsync(HttpMethod.Post, "/api/v1/catalog/services",
            new CreateServiceRequest("Servico Sem Vinculo", 50m, 30));

        var service = await serviceResponse.Content.ReadFromJsonAsync<ServiceResponse>();

        var response = await SendAsync(HttpMethod.Get,
            $"/api/v1/agenda/availability?professionalId={professional}&serviceId={service!.Id}&date=2026-11-16");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var availability = await response.Content.ReadFromJsonAsync<AvailabilityResponse>();

        Assert.Empty(availability!.AvailableSlots);
    }

    [Fact]
    public async Task Agenda_Sem_Servico_Retorna_400_E_Nao_Lista_Vazia()
    {
        // Requisição incompleta não é "sem horários": é 400. Confundir os dois
        // faria o cliente achar que a agenda está cheia quando o pedido está
        // errado.
        var professional = await CreateProfessionalAsync();

        var response = await SendAsync(HttpMethod.Get,
            $"/api/v1/agenda/availability?professionalId={professional}&date=2026-11-16");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(body);
        Assert.Contains("serviço", body!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Agenda_Sem_Profissional_Retorna_400()
    {
        var response = await SendAsync(HttpMethod.Get,
            $"/api/v1/agenda/availability?serviceId={Guid.NewGuid()}&date=2026-11-16");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- F13.7: financeiro ----------

    [Fact]
    public async Task Financeiro_Registra_Pagamento()
    {
        var response = await SendAsync(HttpMethod.Post, "/api/v1/finance/payments",
            new RegisterPaymentRequest(null, 100m, Domain.Finance.PaymentMethod.Cash));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Financeiro_Gera_Relatorio()
    {
        await SendAsync(HttpMethod.Post, "/api/v1/finance/transactions",
            new CreateTransactionRequest(Domain.Finance.TransactionType.Income, 200m, "Receita"));

        var response = await SendAsync(HttpMethod.Get,
            "/api/v1/finance/report?from=2026-11-01&to=2026-11-30");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var report = await response.Content.ReadFromJsonAsync<ReportResponse>();

        Assert.NotNull(report);
        Assert.True(report!.TotalIncome >= 200m);
    }

    // ---------- F13.9: modulos ----------

    [Fact]
    public async Task Modulos_Cria_Pacote()
    {
        var response = await SendAsync(HttpMethod.Post, "/api/v1/modules/packages",
            new CreatePackageRequest("Pacote 10", 400m, 10, 180));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Modulos_Credita_Pontos_De_Fidelidade()
    {
        // A FK composta exige um cliente real do mesmo tenant.
        var created = await SendAsync(HttpMethod.Post, "/api/v1/customers",
            new CreateCustomerRequest("Cliente Fidelidade", "21999990000"));

        var customer = await created.Content.ReadFromJsonAsync<CustomerResponse>();
        var customerId = customer!.Id;

        var response = await SendAsync(HttpMethod.Post,
            $"/api/v1/modules/loyalty/{customerId}/earn", new EarnPointsRequest(50));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var loyalty = await response.Content.ReadFromJsonAsync<LoyaltyResponse>();
        Assert.Equal(50, loyalty!.Points);
    }

    [Fact]
    public async Task Modulos_Calcula_Nps()
    {
        var response = await SendAsync(HttpMethod.Get,
            "/api/v1/modules/nps?from=2026-11-01&to=2026-11-30");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---------- F13.10: paginacao ----------

    [Fact]
    public async Task Paginacao_Limita_O_Tamanho_Maximo_No_Servidor()
    {
        var response = await SendAsync(HttpMethod.Get, "/api/v1/catalog/services?page=1&pageSize=500");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedResponse<ServiceResponse>>();

        // O servidor limita ao maximo: o cliente nao consegue pedir tudo.
        Assert.Equal(PageRequest.MaxPageSize, page!.PageSize);
    }

    [Fact]
    public async Task Paginacao_Normaliza_Pagina_Invalida()
    {
        var response = await SendAsync(HttpMethod.Get, "/api/v1/catalog/services?page=0&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedResponse<ServiceResponse>>();

        Assert.Equal(1, page!.Page);
    }

    [Fact]
    public async Task Paginacao_Retorna_Metadados()
    {
        var response = await SendAsync(HttpMethod.Get, "/api/v1/catalog/services?page=1&pageSize=5");

        var page = await response.Content.ReadFromJsonAsync<PagedResponse<ServiceResponse>>();

        Assert.NotNull(page);
        Assert.Equal(1, page!.Page);
        Assert.Equal(5, page.PageSize);
    }

    // ---------- F13.11: versionamento ----------

    [Fact]
    public async Task Endpoints_Usam_Versionamento_Na_Rota()
    {
        var response = await SendAsync(HttpMethod.Get, "/api/v1/catalog/services");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---------- Autorizacao ----------

    [Fact]
    public async Task Endpoint_Sem_Token_Retorna_401()
    {
        var response = await _client.GetAsync("/api/v1/catalog/services");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<Guid> CreateProfessionalAsync()
    {
        var response = await SendAsync(HttpMethod.Post, "/api/v1/team/professionals",
            new CreateProfessionalRequest(
                "Prof", [DayOfWeek.Monday, DayOfWeek.Tuesday],
                new TimeOnly(9, 0), new TimeOnly(18, 0)));

        var professional = await response.Content.ReadFromJsonAsync<ProfessionalResponse>();

        return professional!.Id;
    }

    /// <summary>
    /// Cria um serviço e um profissional vinculado a ele.
    ///
    /// A disponibilidade passou a depender do serviço: um profissional que
    /// não o executa não tem horário a oferecer. Os testes de consulta
    /// precisam do vínculo para exercitar a jornada.
    /// </summary>
    private async Task<(Guid ProfessionalId, Guid ServiceId)> CreateLinkedProfessionalAsync()
    {
        var serviceResponse = await SendAsync(HttpMethod.Post, "/api/v1/catalog/services",
            new CreateServiceRequest("Servico Agenda", 50m, 30));

        var service = await serviceResponse.Content.ReadFromJsonAsync<ServiceResponse>();

        var professionalId = await CreateProfessionalAsync();

        var link = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/team/professionals/{professionalId}/services/{service!.Id}",
            body: null);

        Assert.True(link.IsSuccessStatusCode, $"Vínculo falhou: {link.StatusCode}");

        return (professionalId, service.Id);
    }
}
