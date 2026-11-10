using Servia.Domain.Common;

namespace Servia.Domain.Operations;

/// <summary>
/// Regra de alerta operacional (F9.2).
///
/// Define quando um alerta deve disparar: taxa de erro acima do limite ou
/// indisponibilidade detectada. A avaliação é uma função pura das métricas
/// observadas — testável sem esperar por um incidente real.
/// </summary>
public sealed class AlertRule
{
    private AlertRule(
        string name,
        AlertKind kind,
        decimal errorRateThreshold,
        int minimumSamples)
    {
        Name = name;
        Kind = kind;
        ErrorRateThreshold = errorRateThreshold;
        MinimumSamples = minimumSamples;
    }

    public string Name { get; }

    public AlertKind Kind { get; }

    /// <summary>Taxa de erro, em percentual, que dispara o alerta.</summary>
    public decimal ErrorRateThreshold { get; }

    /// <summary>
    /// Amostras mínimas antes de avaliar. Evita alertar por um único erro
    /// em uma janela com pouco tráfego.
    /// </summary>
    public int MinimumSamples { get; }

    public static AlertRule ForErrorRate(
        string name,
        decimal errorRateThreshold,
        int minimumSamples = 10)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainRuleException("O nome da regra é obrigatório.");
        }

        if (errorRateThreshold is <= 0 or > 100)
        {
            throw new DomainRuleException("A taxa de erro deve estar entre 0 e 100.");
        }

        if (minimumSamples <= 0)
        {
            throw new DomainRuleException("A amostra mínima deve ser maior que zero.");
        }

        return new AlertRule(name.Trim(), AlertKind.ErrorRate, errorRateThreshold, minimumSamples);
    }

    /// <summary>
    /// Avalia a regra sobre um conjunto de métricas.
    /// Retorna nulo quando não há alerta.
    /// </summary>
    public Alert? Evaluate(IReadOnlyCollection<OperationMetrics> metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        if (metrics.Count < MinimumSamples)
        {
            return null;
        }

        var failures = metrics.Count(metric => !metric.Succeeded);
        var errorRate = Math.Round(failures * 100m / metrics.Count, 2, MidpointRounding.AwayFromZero);

        if (errorRate < ErrorRateThreshold)
        {
            return null;
        }

        return new Alert(
            Name,
            Kind,
            $"Taxa de erro de {errorRate}% em {metrics.Count} operações "
            + $"(limite: {ErrorRateThreshold}%).");
    }
}

/// <summary>Alerta disparado por uma regra.</summary>
public sealed record Alert(string RuleName, AlertKind Kind, string Message);

/// <summary>Tipo de alerta.</summary>
public enum AlertKind
{
    /// <summary>Taxa de erro acima do limite.</summary>
    ErrorRate = 1,

    /// <summary>Serviço indisponível.</summary>
    Unavailability = 2
}
