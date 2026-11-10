using Servia.Domain.Common;
using Servia.Domain.Policies;

namespace Servia.Tests.Policies;

/// <summary>
/// F2.7 — regras de antecedência configuráveis.
/// F2.9 — sinal para reserva.
/// F2.10 — sessões prolongadas.
/// F2.12 — políticas de agendamento.
/// </summary>
public class BookingPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    // ---------- F2.7: antecedencia ----------

    [Fact]
    public void Politica_Padrao_Exige_30_Minutos_De_Antecedencia()
    {
        IBookingPolicy policy = new StandardBookingPolicy();

        Assert.Equal(30, policy.MinimumLeadTimeMinutes);
    }

    [Fact]
    public void Politica_Padrao_Aceita_Horario_Com_Antecedencia_Suficiente()
    {
        IBookingPolicy policy = new StandardBookingPolicy();

        policy.EnsureLeadTime(Now.AddHours(1), Now); // não lança
    }

    [Fact]
    public void Politica_Padrao_Rejeita_Horario_Proximo_Demais()
    {
        IBookingPolicy policy = new StandardBookingPolicy();

        Assert.Throws<DomainRuleException>(() =>
            policy.EnsureLeadTime(Now.AddMinutes(10), Now));
    }

    [Fact]
    public void Politica_De_Sessao_Longa_Exige_24_Horas()
    {
        IBookingPolicy policy = new LongSessionBookingPolicy();

        Assert.Equal(24 * 60, policy.MinimumLeadTimeMinutes);
        Assert.Throws<DomainRuleException>(() =>
            policy.EnsureLeadTime(Now.AddHours(12), Now));
    }

    // ---------- F2.9: sinal ----------

    [Fact]
    public void Politica_Padrao_Nao_Exige_Sinal()
    {
        IBookingPolicy policy = new StandardBookingPolicy();

        Assert.False(policy.RequiresDeposit);
        Assert.Equal(0m, policy.DepositPercentage);
    }

    [Fact]
    public void Politica_De_Sessao_Longa_Exige_Sinal_De_30_Porcento()
    {
        IBookingPolicy policy = new LongSessionBookingPolicy();

        Assert.True(policy.RequiresDeposit);
        Assert.Equal(0.30m, policy.DepositPercentage);
    }

    // ---------- F2.10: sessoes prolongadas ----------

    [Fact]
    public void Politica_Padrao_Limita_Sessao_A_4_Horas()
    {
        IBookingPolicy policy = new StandardBookingPolicy();

        Assert.Equal(4 * 60, policy.MaxSessionMinutes);
        Assert.Throws<DomainRuleException>(() => policy.EnsureSessionDuration(5 * 60));
    }

    [Fact]
    public void Politica_De_Sessao_Longa_Permite_Ate_8_Horas()
    {
        IBookingPolicy policy = new LongSessionBookingPolicy();

        Assert.Equal(8 * 60, policy.MaxSessionMinutes);
        policy.EnsureSessionDuration(6 * 60); // não lança
        Assert.Throws<DomainRuleException>(() => policy.EnsureSessionDuration(9 * 60));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void Ambas_Rejeitam_Duracao_Invalida(int duration)
    {
        IBookingPolicy[] policies = [new StandardBookingPolicy(), new LongSessionBookingPolicy()];

        Assert.All(policies, policy =>
            Assert.Throws<DomainRuleException>(() => policy.EnsureSessionDuration(duration)));
    }

    // ---------- F2.12: contrato polimorfico ----------

    [Fact]
    public void Politicas_Divergem_Para_A_Mesma_Entrada()
    {
        IBookingPolicy standard = new StandardBookingPolicy();
        IBookingPolicy longSession = new LongSessionBookingPolicy();

        // 12 horas de antecedência: aceita na padrão, rejeitada na longa.
        standard.EnsureLeadTime(Now.AddHours(12), Now);
        Assert.Throws<DomainRuleException>(() =>
            longSession.EnsureLeadTime(Now.AddHours(12), Now));

        Assert.NotEqual(standard.Describe(), longSession.Describe());
    }

    [Fact]
    public void Politicas_Declaram_Suas_Regras()
    {
        IBookingPolicy standard = new StandardBookingPolicy();
        IBookingPolicy longSession = new LongSessionBookingPolicy();

        Assert.False(standard.RequiresDeposit);
        Assert.True(longSession.RequiresDeposit);
        Assert.True(longSession.MaxSessionMinutes > standard.MaxSessionMinutes);
    }
}
