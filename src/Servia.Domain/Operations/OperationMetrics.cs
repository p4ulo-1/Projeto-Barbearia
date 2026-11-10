using Servia.Domain.Common;

namespace Servia.Domain.Operations;

/// <summary>
/// Métricas de uma operação, para observabilidade (F9.1).
///
/// Registra duração, resultado e correlação. O <c>TraceId</c> é o que torna
/// uma requisição rastreável de ponta a ponta: o mesmo identificador aparece
/// no log, na métrica e na resposta de erro.
///
/// Objeto de valor: duas métricas com os mesmos valores são equivalentes.
/// </summary>
public readonly record struct OperationMetrics
{
    private OperationMetrics(
        string operationName,
        string traceId,
        TimeSpan duration,
        bool succeeded,
        string? failureReason)
    {
        OperationName = operationName;
        TraceId = traceId;
        Duration = duration;
        Succeeded = succeeded;
        FailureReason = failureReason;
    }

    public string OperationName { get; }

    /// <summary>Identificador de correlação da requisição.</summary>
    public string TraceId { get; }

    public TimeSpan Duration { get; }

    public bool Succeeded { get; }

    public string? FailureReason { get; }

    /// <summary>Indica se a operação excedeu o limite de latência aceitável.</summary>
    public bool IsSlow(TimeSpan threshold) => Duration > threshold;

    public static OperationMetrics RecordSuccess(
        string operationName,
        string traceId,
        TimeSpan duration)
    {
        EnsureValid(operationName, traceId, duration);

        return new OperationMetrics(operationName.Trim(), traceId.Trim(), duration, true, null);
    }

    public static OperationMetrics RecordFailure(
        string operationName,
        string traceId,
        TimeSpan duration,
        string failureReason)
    {
        EnsureValid(operationName, traceId, duration);

        if (string.IsNullOrWhiteSpace(failureReason))
        {
            throw new DomainRuleException("Uma falha deve registrar o motivo.");
        }

        return new OperationMetrics(
            operationName.Trim(), traceId.Trim(), duration, false, failureReason.Trim());
    }

    private static void EnsureValid(string operationName, string traceId, TimeSpan duration)
    {
        if (string.IsNullOrWhiteSpace(operationName))
        {
            throw new DomainRuleException("O nome da operação é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(traceId))
        {
            throw new DomainRuleException("O identificador de correlação é obrigatório.");
        }

        if (duration < TimeSpan.Zero)
        {
            throw new DomainRuleException("A duração não pode ser negativa.");
        }
    }
}
