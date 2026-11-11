using System.Net;
using System.Net.Http.Json;
using Servia.Api.Contracts;
using Servia.Tests.Api;
using Servia.Api.Controllers;

namespace Servia.Tests.Integration;

/// <summary>
/// Etapa 8 — isolamento multi-tenant em leitura, criação, atualização e
/// exclusão, exercitando a API real contra PostgreSQL 17.
///
/// Cada teste cria dados nos dois tenants e verifica que um não enxerga
/// nem altera os do outro.
/// </summary>
public class TenantIsolationIntegrationTests : MultiTenantIntegrationTestBase
{
    public TenantIsolationIntegrationTests(IsolatedApiFactory factory)
        : base(factory)
    {
    }

    /// <summary>Cria um serviço no tenant do token e devolve o identificador.</summary>
    private async Task<Guid> CreateServiceAsync(string token, string name)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/v1/catalog/services", token,
            new CreateServiceRequest(name, 50m, 30));

        var service = await response.Content.ReadFromJsonAsync<ServiceResponse>();

        return service!.Id;
    }

    // ---------- Leitura ----------

    [Fact]
    public async Task Listagem_Retorna_Apenas_Servicos_Do_Tenant_Autenticado()
    {
        var tokenA = await SignInAsTenantAAsync();
        var tokenB = await SignInAsTenantBAsync();

        await CreateServiceAsync(tokenA, "Corte A");
        await CreateServiceAsync(tokenB, "Corte B");

        var responseA = await SendAsync(
            HttpMethod.Get, "/api/v1/catalog/services?page=1&pageSize=50", tokenA);

        var pageA = await responseA.Content.ReadFromJsonAsync<PagedResponse<ServiceResponse>>();

        Assert.Contains(pageA!.Items, item => item.Name == "Corte A");
        Assert.DoesNotContain(pageA.Items, item => item.Name == "Corte B");
    }

    [Fact]
    public async Task Consulta_Por_Id_De_Outro_Tenant_Retorna_404()
    {
        var tokenA = await SignInAsTenantAAsync();
        var tokenB = await SignInAsTenantBAsync();

        var serviceB = await CreateServiceAsync(tokenB, "Servico B");

        // Tenant A tenta alterar o preco de um servico do tenant B.
        var response = await SendAsync(
            HttpMethod.Patch, $"/api/v1/catalog/services/{serviceB}/price", tokenA,
            new ChangePriceRequest(999m));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- Criação ----------

    [Fact]
    public async Task Servico_Criado_Pertence_Ao_Tenant_Do_Token()
    {
        var tokenA = await SignInAsTenantAAsync();

        var serviceId = await CreateServiceAsync(tokenA, "Servico do A");

        var owner = await WithTenantAsync(TenantA, async context =>
            await context.Services.FindAsync(serviceId));

        Assert.NotNull(owner);
        Assert.Equal(TenantA, owner!.TenantId);
    }

    [Fact]
    public async Task Servico_Criado_Em_A_Nao_E_Visivel_Em_B()
    {
        var tokenA = await SignInAsTenantAAsync();
        var tokenB = await SignInAsTenantBAsync();

        var serviceId = await CreateServiceAsync(tokenA, "Exclusivo do A");

        var visibleToB = await WithTenantAsync(TenantB, async context =>
            await context.Services.FindAsync(serviceId));

        Assert.Null(visibleToB);
    }

    // ---------- Atualização ----------

    [Fact]
    public async Task Alteracao_De_Preco_Afeta_Apenas_O_Tenant_Dono()
    {
        var tokenA = await SignInAsTenantAAsync();

        var serviceId = await CreateServiceAsync(tokenA, "Servico A");

        var response = await SendAsync(
            HttpMethod.Patch, $"/api/v1/catalog/services/{serviceId}/price", tokenA,
            new ChangePriceRequest(75m));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await WithTenantAsync(TenantA, async context =>
            await context.Services.FindAsync(serviceId));

        Assert.Equal(75m, updated!.Price.Amount);
    }

    // ---------- Exclusão ----------

    [Fact]
    public async Task Desativacao_De_Servico_De_Outro_Tenant_Retorna_404()
    {
        var tokenA = await SignInAsTenantAAsync();
        var tokenB = await SignInAsTenantBAsync();

        var serviceB = await CreateServiceAsync(tokenB, "Servico B");

        var response = await SendAsync(
            HttpMethod.Post, $"/api/v1/catalog/services/{serviceB}/deactivate", tokenA);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // O servico de B continua ativo.
        var stillActive = await WithTenantAsync(TenantB, async context =>
            await context.Services.FindAsync(serviceB));

        Assert.True(stillActive!.IsActive);
    }

    // ---------- Clientes ----------

    [Fact]
    public async Task Cliente_De_Outro_Tenant_Nao_E_Visivel_Por_Id()
    {
        var tokenA = await SignInAsTenantAAsync();
        var tokenB = await SignInAsTenantBAsync();

        var created = await SendAsync(HttpMethod.Post, "/api/v1/customers", tokenB,
            new CreateCustomerRequest("Cliente B", "21999998888"));

        var customerB = await created.Content.ReadFromJsonAsync<CustomerResponse>();

        var response = await SendAsync(
            HttpMethod.Get, $"/api/v1/customers/{customerB!.Id}", tokenA);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Anonimizacao_De_Cliente_De_Outro_Tenant_Retorna_404()
    {
        var tokenA = await SignInAsTenantAAsync();
        var tokenB = await SignInAsTenantBAsync();

        var created = await SendAsync(HttpMethod.Post, "/api/v1/customers", tokenB,
            new CreateCustomerRequest("Cliente B", "21999997777"));

        var customerB = await created.Content.ReadFromJsonAsync<CustomerResponse>();

        var response = await SendAsync(
            HttpMethod.Post, $"/api/v1/customers/{customerB!.Id}/anonymize", tokenA);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // O cliente de B permanece intacto.
        var intact = await WithTenantAsync(TenantB, async context =>
            await context.Customers.FindAsync(customerB.Id));

        Assert.Equal("Cliente B", intact!.Name);
    }
}
