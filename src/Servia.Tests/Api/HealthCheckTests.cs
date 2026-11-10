using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Servia.Tests.Api;

/// <summary>
/// F0.6 — health check.
///
/// Verifica que o endpoint <c>/health</c> responde com sucesso quando a API
/// e o banco estão operacionais. É o que o orquestrador consulta para saber
/// se a instância pode receber tráfego.
/// </summary>
public class HealthCheckTests : IClassFixture<IsolatedApiFactory>
{
    private readonly HttpClient _client;

    public HealthCheckTests(IsolatedApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_Responde_200_Quando_API_E_Banco_Estao_Operacionais()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Health_Retorna_Corpo_Healthy()
    {
        var response = await _client.GetAsync("/health");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Contains("Healthy", content, StringComparison.OrdinalIgnoreCase);
    }
}
