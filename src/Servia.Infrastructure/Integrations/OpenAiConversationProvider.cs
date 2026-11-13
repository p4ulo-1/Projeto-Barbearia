using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Servia.Domain.Ai;

namespace Servia.Infrastructure.Integrations;

/// <summary>
/// Conduz o diálogo do onboarding sobre a API da OpenAI (item 1.1, 1.2).
///
/// <b>Independência de fornecedor:</b> a regra de negócio depende apenas de
/// <see cref="IAiConversationProvider"/>. Trocar de provedor é implementar o
/// contrato — nenhuma alteração no serviço de onboarding.
///
/// <b>Conversa adaptativa:</b> o prompt recebe o segmento, o histórico
/// completo e o que ainda falta. A IA é instruída a não repetir perguntas e
/// a fazer perguntas específicas do segmento (barbearia, clínica, salão).
///
/// <b>Sem invenção:</b> a IA deve extrair apenas o que o proprietário
/// informou. Fatos ausentes permanecem ausentes e são perguntados.
///
/// <b>Guardrail:</b> a resposta é validada como JSON. Conteúdo inválido
/// falha a operação em vez de aplicar algo que não se entende.
/// </summary>
public sealed class OpenAiConversationProvider : IAiConversationProvider
{
    private const string ApiBaseUrl = "https://api.openai.com/v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly OpenAiOptions _options;
    private readonly RetryPolicy _retryPolicy;

    public OpenAiConversationProvider(
        HttpClient httpClient,
        OpenAiOptions options,
        RetryPolicy? retryPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException("A chave da OpenAI é obrigatória.");
        }

        _httpClient = httpClient;
        _options = options;
        _retryPolicy = retryPolicy ?? new RetryPolicy();

        _httpClient.BaseAddress ??= new Uri(ApiBaseUrl);
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.ApiKey);
    }

    public string ProviderName => "openai";

    public async Task<AiConversationTurn> ContinueAsync(
        AiConversationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var messages = BuildMessages(context);

        var payload = new
        {
            model = _options.Model,
            messages,
            response_format = new { type = "json_object" },
            temperature = 0.6
        };

        try
        {
            var response = await _retryPolicy.ExecuteAsync(
                token => _httpClient.PostAsJsonAsync("/chat/completions", payload, JsonOptions, token),
                result => RetryPolicy.IsTransient((int)result.StatusCode),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync(cancellationToken);

                return AiConversationTurn.Failed($"{(int)response.StatusCode}: {detail}");
            }

            var body = await response.Content.ReadFromJsonAsync<ChatResponse>(JsonOptions, cancellationToken);
            var content = body?.Choices?.FirstOrDefault()?.Message?.Content;

            if (string.IsNullOrWhiteSpace(content))
            {
                return AiConversationTurn.Failed("O provedor não retornou conteúdo.");
            }

            var units = body?.Usage?.TotalTokens ?? 0;

            return ParseTurn(content, units);
        }
        catch (HttpRequestException exception)
        {
            return AiConversationTurn.Failed(exception.Message);
        }
        catch (TaskCanceledException)
        {
            return AiConversationTurn.Failed("A conversa excedeu o tempo limite.");
        }
    }

    /// <summary>
    /// Converte a resposta do modelo em turno validado.
    /// </summary>
    private static AiConversationTurn ParseTurn(string content, int units)
    {
        TurnResponse? turn;

        try
        {
            turn = JsonSerializer.Deserialize<TurnResponse>(content, JsonOptions);
        }
        catch (JsonException)
        {
            return AiConversationTurn.Failed("A resposta do modelo não é JSON válido.");
        }

        if (turn is null)
        {
            return AiConversationTurn.Failed("A resposta do modelo está vazia.");
        }

        var facts = turn.ExtractedFacts ?? new Dictionary<string, string>();

        if (turn.IsComplete)
        {
            return AiConversationTurn.Completed(facts, units);
        }

        if (string.IsNullOrWhiteSpace(turn.Question))
        {
            return AiConversationTurn.Failed(
                "A resposta não trouxe a próxima pergunta nem indicou conclusão.");
        }

        return AiConversationTurn.Asked(turn.Question.Trim(), facts, units);
    }

    private static List<object> BuildMessages(AiConversationContext context)
    {
        var messages = new List<object>
        {
            new { role = "system", content = SystemPrompt(context.Segment) }
        };

        // Resumo dos fatos já conhecidos, para a IA nao repetir perguntas.
        if (context.CollectedFacts.Count > 0)
        {
            var known = string.Join(
                "; ",
                context.CollectedFacts.Select(fact => $"{fact.Key} = {fact.Value}"));

            messages.Add(new
            {
                role = "system",
                content = $"Informações já coletadas: {known}."
            });
        }

        if (context.MissingFacts.Count > 0)
        {
            messages.Add(new
            {
                role = "system",
                content = $"Informações ainda necessárias: {string.Join(", ", context.MissingFacts)}."
            });
        }

        foreach (var message in context.History)
        {
            messages.Add(new
            {
                role = message.Role == AiConversationRole.Assistant ? "assistant" : "user",
                content = message.Content
            });
        }

        return messages;
    }

    private static string SystemPrompt(string segment) =>
        $$"""
        Você configura o site de um estabelecimento do segmento "{{segment}}" por
        conversa. Faça UMA pergunta por vez, curta e específica do segmento.

        Regras:
        - Não repita perguntas cujas respostas já estão nas informações coletadas.
        - Pergunte o que é necessário para montar o site: descrição do
          estabelecimento, endereço, telefone, horário de funcionamento,
          serviços (com preços apenas se informados) e profissionais.
        - Uma barbearia, uma clínica, um salão e um estúdio podem precisar de
          informações diferentes; adapte as perguntas.
        - Nunca invente preços, qualificações, depoimentos ou certificações.
        - Quando houver informação suficiente, marque "isComplete": true e não
          faça pergunta.

        Responda APENAS com JSON neste formato:
        {"question": "próxima pergunta ou null", "extracted_facts": {"chave": "valor"}, "isComplete": false}

        Chaves de fato padronizadas: description, address, phone, openingHours,
        services, professionals. Use outras chaves apenas quando pertinente ao segmento.
        """;

    // ---------- Contratos de resposta do provedor ----------

    private sealed record ChatResponse(List<ChatChoice>? Choices, ChatUsage? Usage);

    private sealed record ChatChoice(ChatMessage? Message);

    private sealed record ChatMessage(string? Content);

    private sealed record ChatUsage(
        [property: JsonPropertyName("total_tokens")] int? TotalTokens);

    private sealed record TurnResponse(
        string? Question,
        [property: JsonPropertyName("extracted_facts")] Dictionary<string, string>? ExtractedFacts,
        [property: JsonPropertyName("isComplete")] bool IsComplete);
}
