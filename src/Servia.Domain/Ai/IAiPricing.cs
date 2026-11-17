namespace Servia.Domain.Ai;

/// <summary>
/// Preços do provedor de IA, para contabilização e estimativa
/// (gestão de IA, §2.1–§2.3).
///
/// <b>Por que uma abstração:</b> a camada de aplicação precisa calcular custo
/// sem depender da configuração concreta da Infrastructure. O domínio define o
/// contrato; a composição fornece os valores configurados.
///
/// <b>Premissa conservadora (§2.1):</b> toda entrada é tratada como cache miss
/// e apenas os preços de pico são aplicados. O preço reduzido de cache hit
/// nunca é usado, mesmo que o provedor informe tokens atendidos por cache.
/// </summary>
public interface IAiPricing
{
    /// <summary>Modelo usado nas operações.</summary>
    string Model { get; }

    /// <summary>Preço por milhão de tokens de entrada, em cache miss.</summary>
    decimal InputPricePerMillionTokens { get; }

    /// <summary>Preço por milhão de tokens de saída.</summary>
    decimal OutputPricePerMillionTokens { get; }

    /// <summary>
    /// Referência da tabela de preços em uso, registrada em cada operação para
    /// auditoria histórica (§2.3).
    /// </summary>
    string PriceTableVersion { get; }

    /// <summary>
    /// Limite máximo de tokens de saída por operação, usado na estimativa
    /// conservadora antes da chamada (§2.3).
    /// </summary>
    int MaxOutputTokens { get; }

    /// <summary>
    /// Calcula o custo conservador a partir dos tokens reais (§2.3).
    /// </summary>
    decimal CalculateCost(long inputTokens, long outputTokens);

    /// <summary>
    /// Estima o custo máximo de uma operação antes da chamada (§2.3).
    /// </summary>
    decimal EstimateMaxCost(int estimatedInputTokens);
}
