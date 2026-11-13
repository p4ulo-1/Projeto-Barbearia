using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Servia.Domain.Ai;
using Servia.Domain.Common;
using Servia.Domain.Sites;

namespace Servia.Infrastructure.Integrations;

/// <summary>
/// Gerador de site por IA sobre a API da OpenAI (F12.3).
///
/// A IA <b>não gera código</b>: devolve uma proposta de seções usando os
/// componentes da biblioteca controlada (ADR-007). A proposta é validada
/// por <see cref="AiChangeSet"/>, que rejeita componente ou propriedade
/// fora do schema.
///
/// Se a resposta não for JSON válido ou violar o schema, a geração falha e
/// o chamador usa o fallback — nunca se aplica uma proposta inválida.
/// </summary>
public sealed class OpenAiSiteGenerator : IAiSiteGenerator
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

    public OpenAiSiteGenerator(
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

    public async Task<AiGenerationResult> GenerateAsync(
        AiGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await CallModelAsync(
            BuildGenerationPrompt(request),
            request,
            cancellationToken);
    }

    public async Task<AiGenerationResult> RefineAsync(
        AiGenerationRequest request,
        AiChangeSet current,
        string instruction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(current);

        if (string.IsNullOrWhiteSpace(instruction))
        {
            throw new DomainRuleException("A instrução de ajuste é obrigatória.");
        }

        return await CallModelAsync(
            BuildRefinementPrompt(request, current, instruction),
            request,
            cancellationToken);
    }

    private async Task<AiGenerationResult> CallModelAsync(
        string prompt,
        AiGenerationRequest request,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            model = _options.Model,
            messages = new[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = prompt }
            },
            response_format = new { type = "json_object" },
            temperature = 0.7
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

                return AiGenerationResult.Failed($"{(int)response.StatusCode}: {detail}");
            }

            var body = await response.Content.ReadFromJsonAsync<ChatResponse>(JsonOptions, cancellationToken);
            var content = body?.Choices?.FirstOrDefault()?.Message?.Content;

            if (string.IsNullOrWhiteSpace(content))
            {
                return AiGenerationResult.Failed("O provedor não retornou conteúdo.");
            }

            var units = body?.Usage?.TotalTokens ?? 0;

            return ParseProposal(content, request, units);
        }
        catch (HttpRequestException exception)
        {
            return AiGenerationResult.Failed(exception.Message);
        }
        catch (TaskCanceledException)
        {
            return AiGenerationResult.Failed("A geração excedeu o tempo limite.");
        }
    }

    /// <summary>
    /// Converte a resposta do modelo em proposta validada.
    /// Qualquer violação do schema rejeita a proposta inteira.
    /// </summary>
    private static AiGenerationResult ParseProposal(
        string content,
        AiGenerationRequest request,
        int units)
    {
        ProposalResponse? proposal;

        try
        {
            proposal = JsonSerializer.Deserialize<ProposalResponse>(content, JsonOptions);
        }
        catch (JsonException)
        {
            return AiGenerationResult.Failed("A resposta do modelo não é JSON válido.");
        }

        if (proposal?.Sections is null || proposal.Sections.Count == 0)
        {
            return AiGenerationResult.Failed("A proposta não contém seções.");
        }

        try
        {
            var theme = proposal.Theme is null
                ? SiteTheme.Default
                : SiteTheme.Create(
                    proposal.Theme.PrimaryColor ?? SiteTheme.Default.PrimaryColor,
                    proposal.Theme.SecondaryColor ?? SiteTheme.Default.SecondaryColor,
                    proposal.Theme.FontFamily ?? SiteTheme.Default.FontFamily,
                    proposal.Theme.LayoutStyle ?? SiteTheme.Default.LayoutStyle);

            var changeSet = AiChangeSet.Create(
                request.TenantId, proposal.Summary ?? $"Site para {request.BusinessName}", theme);

            foreach (var section in proposal.Sections)
            {
                if (!Enum.TryParse<SiteComponentType>(section.Type, ignoreCase: true, out var componentType))
                {
                    return AiGenerationResult.Failed(
                        $"O modelo propôs um componente desconhecido: {section.Type}.");
                }

                changeSet.AddSection(componentType, section.Configuration ?? []);
            }

            return AiGenerationResult.Generated(changeSet, units);
        }
        catch (DomainRuleException exception)
        {
            // Guardrail: proposta fora do schema e rejeitada inteira.
            return AiGenerationResult.Failed(exception.Message);
        }
    }

    private static string BuildGenerationPrompt(AiGenerationRequest request)
    {
        var references = request.ReferenceUrls.Count > 0
            ? string.Join(", ", request.ReferenceUrls)
            : "nenhuma";

        var preferences = request.Preferences ?? "nenhuma";

        return $$"""
            Gere um site para o estabelecimento "{{request.BusinessName}}".
            Segmento: {{request.Segment}}.
            Nível de personalização: {{request.CustomizationLevel}}.
            Preferências: {{preferences}}.
            Referências visuais: {{references}}.

            Responda apenas com JSON no formato:
            {{JsonExample}}
            """;
    }

    private static string BuildRefinementPrompt(
        AiGenerationRequest request,
        AiChangeSet current,
        string instruction)
    {
        var sections = string.Join(", ", current.Sections.Select(section => section.ComponentType));

        return $$"""
            Ajuste o site de "{{request.BusinessName}}" conforme a instrução: {{instruction}}
            Seções atuais: {{sections}}.
            Responda apenas com JSON no mesmo formato da geração inicial.
            """;
    }

    /// <summary>Exemplo de JSON esperado, mantido fora do prompt interpolado.</summary>
    private const string JsonExample =
        """{"summary": "...", "theme": {"primaryColor": "#RRGGBB", "secondaryColor": "#RRGGBB", "fontFamily": "...", "layoutStyle": "..."}, "sections": [{"type": "Hero", "configuration": {"title": "..."}}]}""";

    private const string SystemPrompt =
        """
        Você monta sites para estabelecimentos de serviços usando APENAS os
        componentes da biblioteca: Hero, About, Services, Team, Gallery,
        Testimonials, Contact, BookingWidget, Faq, CallToAction.

        Use somente as propriedades previstas para cada componente. Nunca
        invente propriedades nem componentes. Responda apenas com JSON.
        """;

    // ---------- Contratos de resposta do provedor ----------

    private sealed record ChatResponse(List<ChatChoice>? Choices, ChatUsage? Usage);

    private sealed record ChatChoice(ChatMessage? Message);

    private sealed record ChatMessage(string? Content);

    private sealed record ChatUsage(
        [property: JsonPropertyName("total_tokens")] int? TotalTokens);

    private sealed record ProposalResponse(
        string? Summary,
        ThemeResponse? Theme,
        List<SectionResponse>? Sections);

    private sealed record ThemeResponse(
        string? PrimaryColor,
        string? SecondaryColor,
        string? FontFamily,
        string? LayoutStyle);

    private sealed record SectionResponse(
        string? Type,
        Dictionary<string, string>? Configuration);
}

/// <summary>Configuração da OpenAI.</summary>
public sealed class OpenAiOptions
{
    /// <summary>Chave da API. Deve vir de variável de ambiente ou cofre.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Modelo usado na geração.</summary>
    public string Model { get; set; } = "gpt-4o-mini";
}
