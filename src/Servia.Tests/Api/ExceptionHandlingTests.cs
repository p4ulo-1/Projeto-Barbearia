using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Servia.Api.Contracts;

namespace Servia.Tests.Api;

/// <summary>
/// Verifica o tratamento central de exceções pela API.
/// Cobre os critérios S1-CA, S2-CA e S3-CA (docs/av2/especificacao.md §5).
///
/// Os testes exercitam o pipeline real, com o middleware registrado, e não
/// apenas a classe isolada.
/// </summary>
public class ExceptionHandlingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ExceptionHandlingTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    // ---------- S1: violação de regra de negócio → 400 ----------

    [Fact]
    public async Task RegraDeNegocio_Retorna_400()
    {
        var response = await _client.GetAsync("/api/diagnostics/rule-violation");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(body);
        Assert.Equal("O horário escolhido não está disponível.", body!.Message);
        Assert.Null(body.TraceId);
    }

    [Fact]
    public async Task RegraDeNegocio_Nao_Expoe_Detalhe_Interno()
    {
        var response = await _client.GetAsync("/api/diagnostics/rule-violation");
        var content = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("DomainRuleException", content);
        Assert.DoesNotContain("at Servia", content);
        Assert.DoesNotContain(".cs", content);
    }

    // ---------- S2: recurso inexistente → 404 ----------

    [Fact]
    public async Task RecursoInexistente_Retorna_404()
    {
        var response = await _client.GetAsync("/api/diagnostics/not-found");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(body);
        Assert.Equal("Recurso não encontrado.", body!.Message);
    }

    [Fact]
    public async Task RecursoInexistente_Nao_Expoe_Identificador_Interno()
    {
        var response = await _client.GetAsync("/api/diagnostics/not-found");
        var content = await response.Content.ReadAsStringAsync();

        // A mensagem é genérica: não revela qual recurso nem a chave consultada.
        Assert.DoesNotContain("Agendamento", content);
        Assert.DoesNotContain("00000000", content);
    }

    // ---------- S3: falha inesperada → 500 ----------

    [Fact]
    public async Task FalhaInesperada_Retorna_500_Sem_Vazar_Detalhe()
    {
        var response = await _client.GetAsync("/api/diagnostics/unexpected");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();

        // O detalhe interno NUNCA pode aparecer na resposta.
        Assert.DoesNotContain("connection string secreta", content);
        Assert.DoesNotContain("InvalidOperationException", content);
        Assert.DoesNotContain("at Servia", content);
        Assert.DoesNotContain(".cs", content);
    }

    [Fact]
    public async Task FalhaInesperada_Retorna_TraceId_Para_Correlacao()
    {
        var response = await _client.GetAsync("/api/diagnostics/unexpected");

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.TraceId));
        Assert.Contains("erro inesperado", body.Message);
    }

    // ---------- Contrato de erro ----------

    [Fact]
    public async Task Resposta_De_Erro_Usa_ContentType_Json()
    {
        var response = await _client.GetAsync("/api/diagnostics/rule-violation");

        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }
}
