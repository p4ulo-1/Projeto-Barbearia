using Servia.Domain.Common;
using Servia.Domain.Operations;

namespace Servia.Tests.Operations;

/// <summary>
/// F9.1 — observabilidade: métricas e correlação.
/// F9.2 — alertas de erro e indisponibilidade.
/// </summary>
public class ObservabilityTests
{
    private static readonly TimeSpan SlowThreshold = TimeSpan.FromMilliseconds(500);

    private static OperationMetrics Success(string traceId = "trace-1") =>
        OperationMetrics.RecordSuccess("CreateAppointment", traceId, TimeSpan.FromMilliseconds(120));

    private static OperationMetrics Failure(string traceId = "trace-1") =>
        OperationMetrics.RecordFailure(
            "CreateAppointment", traceId, TimeSpan.FromMilliseconds(80), "Timeout no banco");

    // ---------- F9.1: metricas ----------

    [Fact]
    public void Registrar_Operacao_Bem_Sucedida()
    {
        var metrics = Success();

        Assert.Equal("CreateAppointment", metrics.OperationName);
        Assert.Equal("trace-1", metrics.TraceId);
        Assert.True(metrics.Succeeded);
        Assert.Null(metrics.FailureReason);
    }

    [Fact]
    public void Registrar_Falha_Com_Motivo()
    {
        var metrics = Failure();

        Assert.False(metrics.Succeeded);
        Assert.Equal("Timeout no banco", metrics.FailureReason);
    }

    [Fact]
    public void Falha_Sem_Motivo_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            OperationMetrics.RecordFailure(
                "Op", "trace-1", TimeSpan.FromMilliseconds(10), "  "));
    }

    [Fact]
    public void Operacao_Sem_Nome_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            OperationMetrics.RecordSuccess("  ", "trace-1", TimeSpan.FromMilliseconds(10)));
    }

    [Fact]
    public void Operacao_Sem_TraceId_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            OperationMetrics.RecordSuccess("Op", "  ", TimeSpan.FromMilliseconds(10)));
    }

    [Fact]
    public void Duracao_Negativa_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            OperationMetrics.RecordSuccess("Op", "trace-1", TimeSpan.FromMilliseconds(-1)));
    }

    [Fact]
    public void Operacao_Lenta_Eh_Identificada()
    {
        var slow = OperationMetrics.RecordSuccess(
            "Op", "trace-1", TimeSpan.FromMilliseconds(800));

        Assert.True(slow.IsSlow(SlowThreshold));
        Assert.False(Success().IsSlow(SlowThreshold));
    }

    [Fact]
    public void TraceId_Permite_Correlacionar_Operacoes()
    {
        var first = Success("trace-abc");
        var second = Failure("trace-abc");

        Assert.Equal(first.TraceId, second.TraceId);
    }

    // ---------- F9.2: alertas ----------

    [Fact]
    public void Sem_Amostras_Suficientes_Nao_Alerta()
    {
        var rule = AlertRule.ForErrorRate("Erros", 10m, minimumSamples: 10);

        var alert = rule.Evaluate([Failure(), Failure()]);

        Assert.Null(alert);
    }

    [Fact]
    public void Taxa_De_Erro_Abaixo_Do_Limite_Nao_Alerta()
    {
        var rule = AlertRule.ForErrorRate("Erros", 50m, minimumSamples: 10);
        var metrics = Enumerable.Range(0, 10)
            .Select(index => index < 2 ? Failure() : Success())
            .ToArray();

        Assert.Null(rule.Evaluate(metrics));
    }

    [Fact]
    public void Taxa_De_Erro_Acima_Do_Limite_Dispara_Alerta()
    {
        var rule = AlertRule.ForErrorRate("Erros", 50m, minimumSamples: 10);
        var metrics = Enumerable.Range(0, 10)
            .Select(index => index < 6 ? Failure() : Success())
            .ToArray();

        var alert = rule.Evaluate(metrics);

        Assert.NotNull(alert);
        Assert.Equal(AlertKind.ErrorRate, alert!.Kind);
        Assert.Contains("60", alert.Message);
    }

    [Fact]
    public void Taxa_De_Erro_No_Limite_Exato_Dispara()
    {
        var rule = AlertRule.ForErrorRate("Erros", 50m, minimumSamples: 10);
        var metrics = Enumerable.Range(0, 10)
            .Select(index => index < 5 ? Failure() : Success())
            .ToArray();

        Assert.NotNull(rule.Evaluate(metrics));
    }

    [Fact]
    public void Taxa_Fora_Do_Intervalo_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() => AlertRule.ForErrorRate("Erros", 0m));
        Assert.Throws<DomainRuleException>(() => AlertRule.ForErrorRate("Erros", 101m));
    }

    [Fact]
    public void Amostra_Minima_Invalida_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() => AlertRule.ForErrorRate("Erros", 10m, 0));
    }
}
