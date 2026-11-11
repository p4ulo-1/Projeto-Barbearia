using Servia.Application.Jobs;

namespace Servia.Api.Jobs;

/// <summary>
/// Executa as rotinas periódicas em background (F19.1).
///
/// <b>Por que um único serviço:</b> um `BackgroundService` por rotina
/// multiplicaria threads e dificultaria o diagnóstico. Um executor com
/// rotinas registradas é mais simples de operar e de testar.
///
/// <b>Isolamento de falha:</b> o `JobRunner` captura a exceção de cada
/// rotina. Uma rotina quebrada não derruba o processo nem impede as
/// demais — o erro fica no log com o nome da rotina.
///
/// <b>Escopo por execução:</b> cada ciclo cria um escopo próprio, porque
/// o `AppDbContext` é scoped e não pode viver no serviço singleton.
/// </summary>
public sealed class JobHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<JobHostedService> logger) : BackgroundService
{
    /// <summary>Intervalo entre verificações de rotinas vencidas.</summary>
    public static readonly TimeSpan Tick = TimeSpan.FromSeconds(30);

    private readonly Dictionary<string, DateTimeOffset> _lastRuns = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Executor de rotinas iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Encerramento normal.
                break;
            }
            catch (Exception exception)
            {
                // Falha no ciclo inteiro: registra e tenta de novo no
                // proximo tick. O processo nao morre por causa de uma
                // rotina.
                logger.LogError(exception, "Ciclo de rotinas falhou.");
            }

            try
            {
                await Task.Delay(Tick, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Executor de rotinas encerrado.");
    }

    /// <summary>
    /// Executa as rotinas cujo intervalo venceu.
    /// Exposto para teste: permite rodar um ciclo sem esperar o timer.
    /// </summary>
    public async Task<IReadOnlyList<JobExecution>> RunCycleAsync(
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();

        var runner = scope.ServiceProvider.GetRequiredService<JobRunner>();
        var now = DateTimeOffset.UtcNow;

        var executions = await runner.RunDueAsync(now, _lastRuns, cancellationToken);

        foreach (var execution in executions)
        {
            _lastRuns[execution.JobName] = execution.ExecutedAt;

            if (execution.Succeeded)
            {
                logger.LogInformation("Rotina {Job} concluída.", execution.JobName);
            }
            else
            {
                logger.LogError(
                    "Rotina {Job} falhou: {Reason}",
                    execution.JobName,
                    execution.FailureReason);
            }
        }

        return executions;
    }
}
