using System.Net;
using System.Text.Json;
using Servia.Domain.Ai;
using Servia.Domain.Sites;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Integrations;

namespace Servia.Tests.Integrations;

/// <summary>
/// Contrato e configuração da DeepSeek (plano de implementação, Etapa 1).
///
/// Verifica o JSON efetivamente serializado, sem chamada paga: o campo de
/// identificação deve ser `user_id` (contrato oficial), nunca o legado `user`.
/// Verifica também a seleção de chave por ambiente/serviço e a ausência do
/// segredo no payload.
/// </summary>
public class DeepSeekContractTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private const string ValidProposal =
        """{"summary":"Site","theme":{"primaryColor":"#111111","secondaryColor":"#222222","fontFamily":"Inter","layoutStyle":"classic"},"sections":[{"type":"Hero","configuration":{"title":"Bem-vindo"}}]}""";

    private static string ChatBody(string content, int promptTokens = 100, int completionTokens = 50) =>
        JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content } } },
            usage = new
            {
                prompt_tokens = promptTokens,
                completion_tokens = completionTokens,
                total_tokens = promptTokens + completionTokens
            }
        });

    private static AiOptions NewOptions(string apiKey = "chave-teste") => new()
    {
        ApiKey = apiKey,
        KeyLabel = "development",
        Environment = "development"
    };

    /// <summary>Resolvedor de identificador para teste: HMAC real, sem persistencia.</summary>
    private static IAiTenantIdentifierResolver NewResolver() =>
        new FakeIdentifierResolver("segredo-teste", "v1");

    private static AiGenerationRequest NewRequest() =>
        AiGenerationRequest.Create(
            TenantId, "Barbearia XYZ", SegmentType.Barbershop, SiteCustomizationLevel.Basic);

    // ---------- Contrato de identificação (§4.2) ----------

    [Fact]
    public async Task Payload_Usa_User_Id_E_Nao_User()
    {
        var handler = new StubHandler(HttpStatusCode.OK, ChatBody(ValidProposal));
        var generator = new DeepSeekSiteGenerator(
            new HttpClient(handler), NewOptions(), NewResolver(), new RetryPolicy(maxAttempts: 1));

        await generator.GenerateAsync(NewRequest());

        using var body = JsonDocument.Parse(handler.LastRequestBody);

        Assert.True(
            body.RootElement.TryGetProperty("user_id", out var userId),
            "O payload deve conter o campo 'user_id' (contrato oficial da DeepSeek).");

        Assert.False(
            body.RootElement.TryGetProperty("user", out _),
            "O payload nao deve conter o campo legado 'user'.");

        Assert.False(string.IsNullOrWhiteSpace(userId.GetString()));
    }

    [Fact]
    public async Task Conversa_Usa_User_Id_E_Nao_User()
    {
        var handler = new StubHandler(
            HttpStatusCode.OK,
            ChatBody("""{"question":"Qual o endereco?","extracted_facts":{},"isComplete":false}"""));

        var provider = new DeepSeekConversationProvider(
            new HttpClient(handler), NewOptions(), NewResolver(), new RetryPolicy(maxAttempts: 1));

        var context = new AiConversationContext(
            TenantId,
            "Barbearia XYZ",
            "Barbershop",
            [],
            new Dictionary<string, string>(),
            ["address"]);

        await provider.ContinueAsync(context);

        using var body = JsonDocument.Parse(handler.LastRequestBody);

        Assert.True(body.RootElement.TryGetProperty("user_id", out _));
        Assert.False(body.RootElement.TryGetProperty("user", out _));
    }

    [Fact]
    public async Task Payload_Nao_Contem_A_Chave()
    {
        const string secret = "sk-segredo-nao-vaza";

        var handler = new StubHandler(HttpStatusCode.OK, ChatBody(ValidProposal));
        var generator = new DeepSeekSiteGenerator(
            new HttpClient(handler), NewOptions(secret), NewResolver(), new RetryPolicy(maxAttempts: 1));

        await generator.GenerateAsync(NewRequest());

        Assert.DoesNotContain(secret, handler.LastRequestBody, StringComparison.Ordinal);
    }

    // ---------- Preços de pico (§2.1) ----------

    [Fact]
    public void Precos_Padrao_Sao_Os_De_Pico()
    {
        var options = new AiOptions();

        Assert.Equal(0.30m, options.InputPricePerMillionTokens);
        Assert.Equal(1.20m, options.OutputPricePerMillionTokens);
    }

    [Fact]
    public void Custo_De_Um_Milhao_De_Tokens_Entrada()
    {
        var options = new AiOptions();

        Assert.Equal(0.30m, options.CalculateCost(1_000_000, 0));
    }

    [Fact]
    public void Custo_De_Um_Milhao_De_Tokens_Saida()
    {
        var options = new AiOptions();

        Assert.Equal(1.20m, options.CalculateCost(0, 1_000_000));
    }

    [Fact]
    public void Custo_Estimado_Medio_Usa_Proporcao_40_60()
    {
        var options = new AiOptions();

        // 1M tokens: 400k entrada (0,12) + 600k saida (0,72) = 0,84.
        Assert.Equal(0.84m, options.EstimateAverageCost(1_000_000));
    }

    [Fact]
    public void Estimativa_Maxima_Usa_O_Teto_De_Saida()
    {
        var options = new AiOptions { MaxOutputTokens = 1_000_000 };

        // 1M entrada + 1M saida = 0,30 + 1,20.
        Assert.Equal(1.50m, options.EstimateMaxCost(1_000_000));
    }

    [Fact]
    public void Custo_Negativo_Retorna_Zero()
    {
        var options = new AiOptions();

        Assert.Equal(0m, options.CalculateCost(-1, 0));
    }

    // ---------- Seleção de chave por ambiente e serviço (§3.3) ----------

    [Fact]
    public void Sem_Chave_De_Servico_Usa_A_Chave_Padrao()
    {
        var options = NewOptions("padrao");

        var key = options.ResolveKey(AiServices.SiteGeneration);

        Assert.Equal("padrao", key.ApiKey);
        Assert.Equal("development", key.KeyLabel);
    }

    [Fact]
    public void Servico_Com_Chave_Propria_Usa_A_Chave_Do_Servico()
    {
        var options = NewOptions("padrao");
        options.ServiceKeys[AiServices.OnboardingConversation] = new AiServiceKey
        {
            ApiKey = "chave-conversa",
            KeyLabel = "development-conversation"
        };

        var conversation = options.ResolveKey(AiServices.OnboardingConversation);
        var generation = options.ResolveKey(AiServices.SiteGeneration);

        Assert.Equal("chave-conversa", conversation.ApiKey);
        Assert.Equal("development-conversation", conversation.KeyLabel);

        // O outro servico continua usando a chave padrao.
        Assert.Equal("padrao", generation.ApiKey);
    }

    [Fact]
    public void Servico_Com_Chave_Vazia_Cai_Na_Chave_Padrao()
    {
        var options = NewOptions("padrao");
        options.ServiceKeys[AiServices.SiteGeneration] = new AiServiceKey
        {
            ApiKey = string.Empty,
            KeyLabel = "vazia"
        };

        Assert.Equal("padrao", options.ResolveKey(AiServices.SiteGeneration).ApiKey);
    }

    [Fact]
    public void IsConfiguredFor_Reflete_A_Chave_Do_Servico()
    {
        var options = NewOptions("padrao");
        options.ServiceKeys[AiServices.OnboardingConversation] = new AiServiceKey
        {
            ApiKey = "chave-conversa",
            KeyLabel = "conversation"
        };

        Assert.True(options.IsConfiguredFor(AiServices.SiteGeneration));
        Assert.True(options.IsConfiguredFor(AiServices.OnboardingConversation));

        var semChave = new AiOptions();
        Assert.False(semChave.IsConfiguredFor(AiServices.SiteGeneration));
    }

    [Fact]
    public void Sem_Chave_O_Provedor_Falha_Com_Motivo_Explicito()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new DeepSeekSiteGenerator(
                new HttpClient(new StubHandler(HttpStatusCode.OK, "{}")),
                new AiOptions(),
                NewResolver()));
    }

    [Fact]
    public void Chave_Do_Servico_Nao_Exige_Chave_Padrao()
    {
        var options = new AiOptions();
        options.ServiceKeys[AiServices.SiteGeneration] = new AiServiceKey
        {
            ApiKey = "chave-geracao",
            KeyLabel = "generation"
        };

        // Nao lanca: o servico tem chave propria.
        var generator = new DeepSeekSiteGenerator(
            new HttpClient(new StubHandler(HttpStatusCode.OK, "{}")),
            options,
            NewResolver(),
            new RetryPolicy(maxAttempts: 1));

        Assert.Equal("generation", generator.KeyLabel);
    }
}

/// <summary>Resolvedor de identificador em memoria, para testes de contrato.</summary>
internal sealed class FakeIdentifierResolver(string secret, string keyVersion)
    : IAiTenantIdentifierResolver
{
    public Task<string> ResolveAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(AiUserIdentifier.ForTenant(tenantId, secret, keyVersion));
}
