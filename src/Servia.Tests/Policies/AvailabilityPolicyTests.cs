using Servia.Domain.Appointments;
using Servia.Domain.Policies;

namespace Servia.Tests.Policies;

/// <summary>
/// Verifica o contrato polimórfico IAvailabilityPolicy e suas duas implementações.
/// Critérios: A08 (polimorfismo real) e B06 (contrato + duas implementações).
/// </summary>
public class AvailabilityPolicyTests
{
    private static readonly DayOfWeek[] Weekdays =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    private static WorkSchedule Schedule() =>
        WorkSchedule.Create(Weekdays, new TimeOnly(9, 0), new TimeOnly(18, 0));

    private static DateOnly Monday() => new(2026, 11, 9);

    private static DateOnly Sunday() => new(2026, 11, 8);

    [Fact]
    public void Contrato_Possui_Duas_Implementacoes_Distintas()
    {
        IAvailabilityPolicy standard = new StandardAvailabilityPolicy();
        IAvailabilityPolicy extended = new ExtendedAvailabilityPolicy();

        Assert.NotEqual(standard.Describe(), extended.Describe());
    }

    [Fact]
    public void Standard_Aceita_Horario_Que_Cabe_Na_Jornada()
    {
        IAvailabilityPolicy policy = new StandardAvailabilityPolicy();

        Assert.True(policy.IsAvailable(Schedule(), Monday(), new TimeOnly(10, 0), 60));
    }

    [Fact]
    public void Standard_Rejeita_Horario_Que_Ultrapassa_Fim_Da_Jornada()
    {
        IAvailabilityPolicy policy = new StandardAvailabilityPolicy();

        Assert.False(policy.IsAvailable(Schedule(), Monday(), new TimeOnly(17, 30), 60));
    }

    [Fact]
    public void Extended_Aceita_Horario_Que_Ultrapassa_Fim_Dentro_Da_Tolerancia()
    {
        IAvailabilityPolicy policy = new ExtendedAvailabilityPolicy();

        // 17:30 + 35 min = 18:05, dentro da tolerância de 5 minutos.
        Assert.True(policy.IsAvailable(Schedule(), Monday(), new TimeOnly(17, 30), 35));
    }

    [Fact]
    public void Extended_Rejeita_Horario_Que_Ultrapassa_Alem_Da_Tolerancia()
    {
        IAvailabilityPolicy policy = new ExtendedAvailabilityPolicy();

        // 17:30 + 60 min = 18:30, além da tolerância.
        Assert.False(policy.IsAvailable(Schedule(), Monday(), new TimeOnly(17, 30), 60));
    }

    [Fact]
    public void Implementacoes_Divergem_Para_A_Mesma_Entrada()
    {
        IAvailabilityPolicy standard = new StandardAvailabilityPolicy();
        IAvailabilityPolicy extended = new ExtendedAvailabilityPolicy();
        var schedule = Schedule();
        var time = new TimeOnly(17, 30);
        const int duration = 35;

        var standardResult = standard.IsAvailable(schedule, Monday(), time, duration);
        var extendedResult = extended.IsAvailable(schedule, Monday(), time, duration);

        Assert.False(standardResult);
        Assert.True(extendedResult);
    }

    [Fact]
    public void Ambas_Rejeitam_Dia_Nao_Trabalhado()
    {
        IAvailabilityPolicy[] policies = [new StandardAvailabilityPolicy(), new ExtendedAvailabilityPolicy()];

        Assert.All(policies, policy =>
            Assert.False(policy.IsAvailable(Schedule(), Sunday(), new TimeOnly(10, 0), 30)));
    }

    [Fact]
    public void Ambas_Rejeitam_Duracao_Invalida()
    {
        IAvailabilityPolicy[] policies = [new StandardAvailabilityPolicy(), new ExtendedAvailabilityPolicy()];

        Assert.All(policies, policy =>
            Assert.False(policy.IsAvailable(Schedule(), Monday(), new TimeOnly(10, 0), 0)));
    }
}
