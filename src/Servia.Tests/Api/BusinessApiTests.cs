using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Servia.Api.Contracts;
using Servia.Api.Controllers;
using Servia.Domain.Identity;
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
    public async Task Catalogo_Cria_Categoria()
    {
        var response = await SendAsync(HttpMethod.Post, "/api/v1/catalog/categories",
            new CreateCategoryRequest("Cortes", 1));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
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
        var professional = await CreateProfessionalAsync();

        var response = await SendAsync(HttpMethod.Get,
            $"/api/v1/agenda/availability?professionalId={professional}&date=2026-11-16&durationMinutes=30");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var availability = await response.Content.ReadFromJsonAsync<AvailabilityResponse>();

        Assert.NotNull(availability);
        Assert.NotEmpty(availability!.AvailableSlots);
    }

    [Fact]
    public async Task Agenda_Nao_Oferece_Horario_Em_Dia_Nao_Trabalhado()
    {
        var professional = await CreateProfessionalAsync();

        // 2026-11-15 e domingo; a jornada e segunda e terca.
        var response = await SendAsync(HttpMethod.Get,
            $"/api/v1/agenda/availability?professionalId={professional}&date=2026-11-15&durationMinutes=30");

        var availability = await response.Content.ReadFromJsonAsync<AvailabilityResponse>();

        Assert.Empty(availability!.AvailableSlots);
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
        var customerId = Guid.NewGuid();

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
}
