namespace Servia.Application.Jobs;

/// <summary>
/// Contrato de uma rotina periódica (F19.1).
///
/// Cada rotina é independente: uma falha não impede as demais de rodar.
/// O executor captura a exceção, registra e segue para a próxima.
/// </summary>
public interface IScheduledJob
{
    /// <summary>Nome da rotina, usado em log e diagnóstico.</summary>
    string Name { get; }

    /// <summary>Intervalo entre execuções.</summary>
    TimeSpan Interval { get; }

    /// <summary>Executa a rotina.</summary>
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Executa as rotinas periódicas (F19.1).
///
/// <b>Isolamento de falha:</b> cada rotina roda em try/catch próprio. Uma
/// rotina quebrada não impede as outras — e o erro fica no log com o nome
/// da rotina, não como falha genérica do processo.
///
/// <b>Por que não um job por `BackgroundService`:</b> um serviço por
/// rotina multiplicaria o consumo de threads e dificultaria o diagnóstico.
/// Um executor único com rotinas registradas é mais simples de operar.
/// </summary>
public sealed class JobRunner(IEnumerable<IScheduledJob> jobs)
{
    /// <summary>
    /// Executa todas as rotinas cujo intervalo já venceu.
    /// Retorna o resultado de cada uma.
    /// </summary>
    public async Task<IReadOnlyList<JobExecution>> RunDueAsync(
        DateTimeOffset now,
        IReadOnlyDictionary<string, DateTimeOffset> lastRuns,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lastRuns);

        var executions = new List<JobExecution>();

        foreach (var job in jobs)
        {
            var lastRun = lastRuns.TryGetValue(job.Name, out var value) ? value : DateTimeOffset.MinValue;

            if (now - lastRun < job.Interval)
            {
                continue;
            }

            executions.Add(await RunOneAsync(job, now, cancellationToken));
        }

        return executions;
    }

    /// <summary>
    /// Executa uma rotina, capturando a falha sem interromper as demais.
    /// </summary>
    public async Task<JobExecution> RunOneAsync(
        IScheduledJob job,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        try
        {
            await job.ExecuteAsync(cancellationToken);

            return new JobExecution(job.Name, true, null, now);
        }
        catch (OperationCanceledException)
        {
            // Cancelamento nao e falha: o processo esta encerrando.
            throw;
        }
        catch (Exception exception)
        {
            // O erro e devolvido no resultado: quem executa decide como
            // registrar. A aplicacao nao depende de logging.
            return new JobExecution(job.Name, false, exception.Message, now);
        }
    }
}

/// <summary>Resultado da execução de uma rotina.</summary>
public sealed record JobExecution(
    string JobName,
    bool Succeeded,
    string? FailureReason,
    DateTimeOffset ExecutedAt);
