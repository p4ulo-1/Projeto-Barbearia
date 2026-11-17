using Servia.Domain.Ai;

namespace Servia.Infrastructure.Integrations;

/// <summary>
/// Configuração do provedor de IA (gestão de IA, §2 e §3).
///
/// <b>Provedor único e centralizado:</b> o Servia usa uma conta única na
/// DeepSeek para todos os estabelecimentos. Os tenants não criam conta nem
/// fornecem chave — a credencial fica exclusivamente no backend.
///
/// <b>Nada fixo no código:</b> endereço, modelo e preços vêm de configuração,
/// para que trocar de modelo ou de provedor não exija recompilar regra de
/// negócio (§2).
///
/// <b>Preços de pico (§2.1):</b> a contabilização usa exclusivamente os preços
/// de pico publicados pela DeepSeek. Toda entrada é tratada como cache miss —
/// o preço reduzido de cache hit nunca é aplicado, mesmo que o provedor
/// informe tokens atendidos por cache.
///
/// <b>Chaves por ambiente e serviço (§3.3):</b> a chave é escolhida pelo
/// backend a partir do ambiente e do serviço da operação. Nunca é escolhida
/// pelo frontend nem pelo tenant, e nunca existe uma chave por estabelecimento.
///
/// <b>Segredo:</b> <see cref="ApiKey"/> vem de variável de ambiente ou cofre.
/// Nunca é registrada em log, resposta de API ou documento (§3.4, §15).
/// </summary>
public sealed class AiOptions : IAiPricing, IAiIdentifierOptions
{
    /// <summary>Nome do provedor, para log e auditoria.</summary>
    public string ProviderName { get; set; } = "deepseek";

    /// <summary>Endereço base da API do provedor.</summary>
    public string BaseUrl { get; set; } = "https://api.deepseek.com/v1";

    /// <summary>
    /// Identificador interno do modelo (gestão de IA, §2).
    /// </summary>
    public string Model { get; set; } = "deepseek-flash";

    /// <summary>
    /// Ambiente da operação: `development`, `staging` ou `production`.
    /// Usado para selecionar a chave e para auditoria.
    /// </summary>
    public string Environment { get; set; } = "development";

    /// <summary>
    /// Chave da API do serviço padrão. Deve vir de variável de ambiente ou
    /// cofre. Mantida para compatibilidade com a configuração de chave única.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Identificação da chave para auditoria — ambiente e serviço a que ela
    /// pertence (§3.3, §5.4). Nunca contém o segredo.
    /// </summary>
    public string KeyLabel { get; set; } = "development";

    /// <summary>
    /// Preço por milhão de tokens de entrada, em cache miss (§2.1).
    /// Padrão: US$ 0,30.
    /// </summary>
    public decimal InputPricePerMillionTokens { get; set; } = 0.30m;

    /// <summary>
    /// Preço por milhão de tokens de saída (§2.1). Padrão: US$ 1,20.
    /// </summary>
    public decimal OutputPricePerMillionTokens { get; set; } = 1.20m;

    /// <summary>
    /// Versão/referência da tabela de preços em uso (§2.3). Registrada em cada
    /// operação para permitir auditoria histórica quando os preços mudarem.
    /// </summary>
    public string PriceTableVersion { get; set; } = "deepseek-peak-2026-11";

    /// <summary>
    /// Proporção estimada de tokens de entrada para pré-cálculo (§2.2).
    /// Usada apenas em estimativas; nunca substitui os tokens reais.
    /// </summary>
    public decimal EstimatedInputRatio { get; set; } = 0.40m;

    /// <summary>
    /// Limite máximo de tokens de saída por operação, usado na estimativa
    /// conservadora de custo antes da chamada (§2.3).
    /// </summary>
    public int MaxOutputTokens { get; set; } = 4_000;

    /// <summary>
    /// Chaves por serviço (§3.3). Quando ausente, usa <see cref="ApiKey"/>.
    /// A seleção é feita pelo backend, nunca pelo tenant.
    /// </summary>
    public Dictionary<string, AiServiceKey> ServiceKeys { get; set; } = [];

    /// <summary>
    /// Segredo do HMAC usado no identificador opaco enviado ao provedor
    /// (§4.2). Exclusivo do backend, fora do Git. Nunca é registrado em log.
    /// </summary>
    public string UserIdentifierSecret { get; set; } = string.Empty;

    /// <summary>
    /// Versão da chave HMAC em uso (§4.2). Registrada com cada identificador
    /// para permitir rotação rastreável.
    /// </summary>
    public string UserIdentifierKeyVersion { get; set; } = "v1";

    /// <summary>Segredo do HMAC exposto ao resolvedor de identificador.</summary>
    string IAiIdentifierOptions.Secret => UserIdentifierSecret;

    /// <summary>Versão da chave exposta ao resolvedor de identificador.</summary>
    string IAiIdentifierOptions.KeyVersion => UserIdentifierKeyVersion;

    /// <summary>
    /// Indica se a credencial do serviço padrão está presente. Sem ela, a IA
    /// fica indisponível e a aplicação sobe normalmente (§3.4).
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    /// <summary>
    /// Resolve a chave do serviço informado. Retorna a chave específica do
    /// serviço quando configurada; caso contrário, a chave padrão.
    ///
    /// A seleção depende apenas do serviço e do ambiente — nunca do tenant.
    /// </summary>
    public AiServiceKey ResolveKey(string service)
    {
        if (!string.IsNullOrWhiteSpace(service)
            && ServiceKeys.TryGetValue(service, out var specific)
            && !string.IsNullOrWhiteSpace(specific.ApiKey))
        {
            return specific;
        }

        return new AiServiceKey
        {
            ApiKey = ApiKey,
            KeyLabel = KeyLabel
        };
    }

    /// <summary>
    /// Indica se há credencial utilizável para o serviço informado.
    /// </summary>
    public bool IsConfiguredFor(string service) =>
        !string.IsNullOrWhiteSpace(ResolveKey(service).ApiKey);

    /// <summary>
    /// Calcula o custo conservador da operação a partir dos tokens reais
    /// retornados pelo provedor (§2.3).
    ///
    /// Fórmula: `(entrada × preço_entrada / 1.000.000) + (saída × preço_saída / 1.000.000)`.
    /// Toda entrada é tratada como cache miss.
    /// </summary>
    public decimal CalculateCost(long inputTokens, long outputTokens)
    {
        if (inputTokens < 0 || outputTokens < 0)
        {
            return 0m;
        }

        const decimal million = 1_000_000m;

        return (inputTokens / million * InputPricePerMillionTokens)
               + (outputTokens / million * OutputPricePerMillionTokens);
    }

    /// <summary>
    /// Estima o custo máximo de uma operação antes da chamada (§2.3).
    ///
    /// Usa a estimativa de entrada e o limite máximo de saída permitido. A
    /// estimativa é conservadora: considera toda a entrada como cache miss e o
    /// teto de saída, não a expectativa média.
    /// </summary>
    public decimal EstimateMaxCost(int estimatedInputTokens)
    {
        var input = Math.Max(0, estimatedInputTokens);
        var output = Math.Max(0, MaxOutputTokens);

        return CalculateCost(input, output);
    }

    /// <summary>
    /// Estima o custo médio de uma quantidade de tokens usando a proporção
    /// 40/60 (§2.2). Usado apenas para pré-cálculo, nunca para contabilização.
    /// </summary>
    public decimal EstimateAverageCost(long totalTokens)
    {
        if (totalTokens <= 0)
        {
            return 0m;
        }

        var ratio = Math.Clamp(EstimatedInputRatio, 0m, 1m);
        var input = (long)(totalTokens * ratio);
        var output = totalTokens - input;

        return CalculateCost(input, output);
    }
}

/// <summary>
/// Credencial de um serviço de IA (§3.3). O rótulo identifica a chave para
/// auditoria sem expor o segredo.
/// </summary>
public sealed class AiServiceKey
{
    /// <summary>Chave da API. Vem de variável de ambiente ou cofre.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Rótulo da chave para auditoria, sem o segredo.</summary>
    public string KeyLabel { get; set; } = string.Empty;
}

/// <summary>
/// Serviços de IA que consomem a conta central (§3.3). Cada serviço pode ter
/// chave própria quando houver justificativa técnica.
/// </summary>
public static class AiServices
{
    /// <summary>Geração e ajuste de configuração do site.</summary>
    public const string SiteGeneration = "site-generation";

    /// <summary>Conversa do onboarding assistido.</summary>
    public const string OnboardingConversation = "onboarding-conversation";
}
