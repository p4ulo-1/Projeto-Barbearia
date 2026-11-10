using Servia.Domain.Billing;
using Servia.Domain.Common;

namespace Servia.Tests.Billing;

/// <summary>
/// F4.6 — régua de inadimplência: 3 dias de aviso.
/// F4.7 — bloqueio de escrita com leitura por 15 dias.
/// </summary>
public class DelinquencyPolicyTests
{
    private static readonly DateTimeOffset FailedAt = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    // ---------- F4.6: aviso ----------

    [Fact]
    public void No_Dia_Da_Falha_Estado_Eh_Aviso()
    {
        Assert.Equal(DelinquencyState.Warning, DelinquencyPolicy.Evaluate(FailedAt, FailedAt));
    }

    [Fact]
    public void Durante_Os_3_Dias_De_Aviso_Acesso_Eh_Total()
    {
        var state = DelinquencyPolicy.Evaluate(FailedAt, FailedAt.AddDays(2));

        Assert.Equal(DelinquencyState.Warning, state);
        Assert.True(DelinquencyPolicy.CanWrite(state));
        Assert.True(DelinquencyPolicy.CanRead(state));
    }

    [Fact]
    public void Apos_3_Dias_Comeca_O_Bloqueio_De_Escrita()
    {
        var state = DelinquencyPolicy.Evaluate(FailedAt, FailedAt.AddDays(3));

        Assert.Equal(DelinquencyState.WriteBlocked, state);
        Assert.False(DelinquencyPolicy.CanWrite(state));
    }

    // ---------- F4.7: bloqueio de escrita ----------

    [Fact]
    public void Durante_O_Bloqueio_Leitura_E_Agenda_Continuam()
    {
        var state = DelinquencyPolicy.Evaluate(FailedAt, FailedAt.AddDays(10));

        Assert.Equal(DelinquencyState.WriteBlocked, state);
        Assert.False(DelinquencyPolicy.CanWrite(state));
        Assert.True(DelinquencyPolicy.CanRead(state));
    }

    [Fact]
    public void Apos_15_Dias_De_Bloqueio_Tenant_Eh_Suspenso()
    {
        var state = DelinquencyPolicy.Evaluate(FailedAt, FailedAt.AddDays(18));

        Assert.Equal(DelinquencyState.Suspended, state);
        Assert.False(DelinquencyPolicy.CanWrite(state));
        Assert.False(DelinquencyPolicy.CanRead(state));
    }

    [Fact]
    public void Limite_Exato_Da_Suspensao()
    {
        // 3 dias de aviso + 15 de bloqueio = 18 dias.
        Assert.Equal(DelinquencyState.WriteBlocked,
            DelinquencyPolicy.Evaluate(FailedAt, FailedAt.AddDays(17)));

        Assert.Equal(DelinquencyState.Suspended,
            DelinquencyPolicy.Evaluate(FailedAt, FailedAt.AddDays(18)));
    }

    // ---------- Datas calculadas ----------

    [Fact]
    public void Bloqueio_De_Escrita_Comeca_Em_3_Dias()
    {
        Assert.Equal(FailedAt.AddDays(3), DelinquencyPolicy.WriteBlockStartsAt(FailedAt));
    }

    [Fact]
    public void Suspensao_Comeca_Em_18_Dias()
    {
        Assert.Equal(FailedAt.AddDays(18), DelinquencyPolicy.SuspensionStartsAt(FailedAt));
    }

    [Fact]
    public void Instante_Anterior_A_Falha_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            DelinquencyPolicy.Evaluate(FailedAt, FailedAt.AddDays(-1)));
    }

    // ---------- Constantes da regra ----------

    [Fact]
    public void Regua_Usa_Os_Prazos_Definidos()
    {
        Assert.Equal(3, DelinquencyPolicy.GraceDays);
        Assert.Equal(15, DelinquencyPolicy.ReadOnlyDays);
    }
}
