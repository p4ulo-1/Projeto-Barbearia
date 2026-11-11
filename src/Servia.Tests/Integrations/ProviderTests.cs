using System.Net;
using System.Text;
using Servia.Domain.Ai;
using Servia.Domain.Billing;
using Servia.Domain.Common;
using Servia.Domain.Messaging;
using Servia.Domain.Sites;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Integrations;

namespace Servia.Tests.Integrations;

/// <summary>
/// F12.1 — Mercado Pago.
/// F12.2 — Meta WhatsApp.
/// F12.3 — OpenAI.
/// F12.7 — testes com dublês, sem chamar serviço externo.
/// </summary>
public class ProviderTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    // ---------- F12.1: Mercado Pago ----------

    [Fact]
    public async Task MercadoPago_Registra_Cliente()
    {
        var handler = new StubHandler(HttpStatusCode.Created,
            """{"id":"cus_123","card":{"last_four_digits":"4242","payment_method":{"id":"visa"}}}""");

        var gateway = NewMercadoPago(handler);

        var customer = await gateway.RegisterCustomerAsync(TenantId, "a@b.com", "card-token");

        Assert.Equal("cus_123", customer.CustomerId);
        Assert.Equal("4242", customer.CardLastFour);
        Assert.Equal("visa", customer.CardBrand);
    }

    [Fact]
    public async Task MercadoPago_Cria_Assinatura()
    {
        var handler = new StubHandler(HttpStatusCode.Created,
            """{"id":"pre_123","status":"authorized"}""");

        var gateway = NewMercadoPago(handler);

        var subscription = await gateway.CreateSubscriptionAsync(
            "cus_123", Money.From(99m), "idem-1");

        Assert.Equal("pre_123", subscription.SubscriptionId);
        Assert.Equal(GatewaySubscriptionStatus.Active, subscription.Status);
    }

    [Fact]
    public async Task MercadoPago_Envia_Chave_De_Idempotencia()
    {
        var handler = new StubHandler(HttpStatusCode.Created,
            """{"id":"pay_1","status":"approved"}""");

        var gateway = NewMercadoPago(handler);

        await gateway.CreateChargeAsync("cus_123", Money.From(50m), "Cobrança", "idem-abc");

        Assert.Contains("idem-abc", handler.LastRequestHeaders);
    }

    [Fact]
    public async Task MercadoPago_Propaga_Erro_Do_Gateway()
    {
        var handler = new StubHandler(HttpStatusCode.BadRequest, """{"message":"cartão inválido"}""");

        var gateway = NewMercadoPago(handler);

        await Assert.ThrowsAsync<DomainRuleException>(() =>
            gateway.RegisterCustomerAsync(TenantId, "a@b.com", "token"));
    }

    [Fact]
    public void MercadoPago_Sem_Token_Lanca_Excecao()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new MercadoPagoGateway(new HttpClient(new StubHandler(HttpStatusCode.OK, "{}")),
                new MercadoPagoOptions()));
    }

    // ---------- F12.2: Meta WhatsApp ----------

    [Fact]
    public async Task WhatsApp_Envia_Texto()
    {
        var handler = new StubHandler(HttpStatusCode.OK,
            """{"messages":[{"id":"wamid.123"}]}""");

        var provider = NewWhatsApp(handler);

        var result = await provider.SendTextAsync("5511999998888", "Olá", "idem-1");

        Assert.True(result.Success);
        Assert.Equal("wamid.123", result.ExternalId);
    }

    [Fact]
    public async Task WhatsApp_Envia_Template()
    {
        var handler = new StubHandler(HttpStatusCode.OK,
            """{"messages":[{"id":"wamid.456"}]}""");

        var provider = NewWhatsApp(handler);

        var result = await provider.SendTemplateAsync(
            "5511999998888", "confirmacao",
            new Dictionary<string, string> { ["1"] = "João" }, "idem-2");

        Assert.True(result.Success);
    }

    [Fact]
    public async Task WhatsApp_Retorna_Falha_Quando_Provedor_Rejeita()
    {
        var handler = new StubHandler(HttpStatusCode.BadRequest, """{"error":"número inválido"}""");

        var provider = NewWhatsApp(handler);

        var result = await provider.SendTextAsync("000", "Olá", "idem-3");

        Assert.False(result.Success);
        Assert.Contains("400", result.FailureReason);
    }

    [Fact]
    public async Task WhatsApp_Nao_Afirma_Template_Aprovado_Em_Falha_De_Rede()
    {
        var handler = new StubHandler(HttpStatusCode.InternalServerError, "erro");

        var provider = NewWhatsApp(handler);

        Assert.False(await provider.IsTemplateApprovedAsync("confirmacao"));
    }

    [Fact]
    public void WhatsApp_Sem_Token_Lanca_Excecao()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new MetaWhatsAppProvider(new HttpClient(new StubHandler(HttpStatusCode.OK, "{}")),
                new MetaWhatsAppOptions()));
    }

    // ---------- F12.3: OpenAI ----------

    [Fact]
    public async Task OpenAi_Gera_Proposta_Valida()
    {
        var proposal = """
            {"summary":"Site clássico","theme":{"primaryColor":"#1F2937","secondaryColor":"#F3F4F6","fontFamily":"Inter","layoutStyle":"classic"},"sections":[{"type":"Hero","configuration":{"title":"Barbearia XYZ"}}]}
            """;

        var handler = new StubHandler(HttpStatusCode.OK, ChatResponse(proposal, 150));

        var generator = NewOpenAi(handler);

        var result = await generator.GenerateAsync(NewRequest());

        Assert.True(result.Success);
        Assert.NotNull(result.ChangeSet);
        Assert.Single(result.ChangeSet!.Sections);
        Assert.Equal(150, result.UnitsConsumed);
    }

    [Fact]
    public async Task OpenAi_Rejeita_Componente_Fora_Da_Biblioteca()
    {
        var proposal = """
            {"summary":"Site","sections":[{"type":"ComponenteInventado","configuration":{"title":"X"}}]}
            """;

        var handler = new StubHandler(HttpStatusCode.OK, ChatResponse(proposal, 100));

        var result = await NewOpenAi(handler).GenerateAsync(NewRequest());

        // Guardrail: proposta invalida e rejeitada inteira.
        Assert.False(result.Success);
        Assert.Contains("desconhecido", result.FailureReason);
    }

    [Fact]
    public async Task OpenAi_Rejeita_Propriedade_Fora_Do_Schema()
    {
        var proposal = """
            {"summary":"Site","sections":[{"type":"Hero","configuration":{"codigoArbitrario":"<script>"}}]}
            """;

        var handler = new StubHandler(HttpStatusCode.OK, ChatResponse(proposal, 100));

        var result = await NewOpenAi(handler).GenerateAsync(NewRequest());

        Assert.False(result.Success);
    }

    [Fact]
    public async Task OpenAi_Rejeita_Resposta_Nao_Json()
    {
        var handler = new StubHandler(HttpStatusCode.OK, ChatResponse("isto não é json", 50));

        var result = await NewOpenAi(handler).GenerateAsync(NewRequest());

        Assert.False(result.Success);
        Assert.Contains("JSON", result.FailureReason);
    }

    [Fact]
    public async Task OpenAi_Propaga_Erro_Do_Provedor()
    {
        var handler = new StubHandler(HttpStatusCode.Unauthorized, """{"error":"chave inválida"}""");

        var result = await NewOpenAi(handler).GenerateAsync(NewRequest());

        Assert.False(result.Success);
        Assert.Contains("401", result.FailureReason);
    }

    [Fact]
    public void OpenAi_Sem_Chave_Lanca_Excecao()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new OpenAiSiteGenerator(new HttpClient(new StubHandler(HttpStatusCode.OK, "{}")),
                new OpenAiOptions()));
    }

    // ---------- Auxiliares ----------

    private static MercadoPagoGateway NewMercadoPago(HttpMessageHandler handler) =>
        new(new HttpClient(handler), new MercadoPagoOptions { AccessToken = "token-teste" },
            new RetryPolicy(maxAttempts: 1));

    private static MetaWhatsAppProvider NewWhatsApp(HttpMessageHandler handler) =>
        new(new HttpClient(handler),
            new MetaWhatsAppOptions { AccessToken = "token-teste", PhoneNumberId = "123" },
            new RetryPolicy(maxAttempts: 1));

    private static OpenAiSiteGenerator NewOpenAi(HttpMessageHandler handler) =>
        new(new HttpClient(handler), new OpenAiOptions { ApiKey = "chave-teste" },
            new RetryPolicy(maxAttempts: 1));

    private static AiGenerationRequest NewRequest() =>
        AiGenerationRequest.Create(
            TenantId, "Barbearia XYZ", SegmentType.Barbershop, SiteCustomizationLevel.Basic);

    private static string ChatResponse(string content, int tokens) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content } } },
            usage = new { total_tokens = tokens }
        });
}

/// <summary>Handler HTTP que devolve uma resposta fixa e registra a requisição.</summary>
internal sealed class StubHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
{
    public string LastRequestHeaders { get; private set; } = string.Empty;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastRequestHeaders = request.Headers.ToString();

        if (request.Content is not null)
        {
            LastRequestHeaders += await request.Content.ReadAsStringAsync(cancellationToken);
        }

        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }
}
