using Servia.Domain.Appointments;
using Servia.Domain.Common;
using Servia.Domain.Policies;

namespace Servia.Tests.Repro;

/// <summary>
/// Testes de regressão do defeito DEF-002.
///
/// O defeito: agendamentos que terminavam após a meia-noite eram aceitos sem
/// qualquer verificação, e o conflito com o dia seguinte não era detectado.
///
/// A correção introduz a política <see cref="IOvernightPolicy"/>: o
/// comportamento passa a depender do segmento, e não de uma regra global.
/// Ver docs/av2/registro-de-depuracao.md.
/// </summary>
public class DefectReproTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid ServiceId = Guid.NewGuid();
    private static readonly Guid ProfessionalId = Guid.NewGuid();
    private static readonly DateOnly Date = new(2026, 11, 9);
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);

    private static Appointment Create(TimeOnly time, int duration, IOvernightPolicy? policy = null) =>
        Appointment.Create(
            TenantId, CustomerId, ServiceId, ProfessionalId,
            Date, time, duration, Now, policy);

    // ---------- Política padrão: proíbe ----------

    [Fact]
    public void DEF002_Sem_Politica_Rejeita_Atendimento_Que_Ultrapassa_Meia_Noite()
    {
        var exception = Assert.Throws<DomainRuleException>(() =>
            Create(new TimeOnly(23, 50), 60));

        Assert.Contains("meia-noite", exception.Message);
    }

    [Fact]
    public void DEF002_NoOvernightPolicy_Rejeita_Atendimento_Que_Ultrapassa_Meia_Noite()
    {
        Assert.Throws<DomainRuleException>(() =>
            Create(new TimeOnly(23, 0), 120, new NoOvernightPolicy()));
    }

    [Fact]
    public void DEF002_NoOvernightPolicy_Aceita_Atendimento_Que_Termina_Na_Meia_Noite()
    {
        var appointment = Create(new TimeOnly(23, 50), 10, new NoOvernightPolicy());

        Assert.Equal(TimeRange.MinutesPerDay, appointment.OccupiedRange.EndMinutes);
    }

    // ---------- Política de sessão longa: permite ----------

    [Fact]
    public void DEF002_AllowOvernightPolicy_Aceita_Atendimento_Que_Ultrapassa_Meia_Noite()
    {
        var appointment = Create(new TimeOnly(23, 50), 60, new AllowOvernightPolicy());

        Assert.Equal(1490, appointment.OccupiedRange.EndMinutes);
        Assert.Equal(Date, appointment.Date);
    }

    [Fact]
    public void DEF002_AllowOvernightPolicy_Rejeita_Sessao_Acima_Do_Limite()
    {
        // 22:00 + 8h = 06:00 do dia seguinte: 6h apos a meia-noite, acima do limite de 4h.
        Assert.Throws<DomainRuleException>(() =>
            Create(new TimeOnly(22, 0), 8 * 60, new AllowOvernightPolicy()));
    }

    // ---------- Contrato polimórfico ----------

    [Fact]
    public void DEF002_Politicas_Divergem_Para_A_Mesma_Entrada()
    {
        IOvernightPolicy strict = new NoOvernightPolicy();
        IOvernightPolicy permissive = new AllowOvernightPolicy();
        var range = TimeRange.From(new TimeOnly(23, 50), 60);

        Assert.Throws<DomainRuleException>(() => strict.EnsureAllowed(range));
        permissive.EnsureAllowed(range); // não lança

        Assert.False(strict.AllowsOvernight);
        Assert.True(permissive.AllowsOvernight);
        Assert.NotEqual(strict.Describe(), permissive.Describe());
    }

    // ---------- Não regressão ----------

    [Fact]
    public void DEF002_Atendimento_Diurno_Continua_Aceito_Em_Ambas_As_Politicas()
    {
        IOvernightPolicy[] policies = [new NoOvernightPolicy(), new AllowOvernightPolicy()];

        Assert.All(policies, policy =>
        {
            var appointment = Create(new TimeOnly(10, 0), 60, policy);
            Assert.Equal(600, appointment.OccupiedRange.StartMinutes);
            Assert.Equal(660, appointment.OccupiedRange.EndMinutes);
        });
    }
}
