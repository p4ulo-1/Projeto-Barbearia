using Servia.Domain.Billing;
using Servia.Domain.Common;

namespace Servia.Tests.Billing;

public sealed class DelinquencyPolicyTests
{
    private static readonly DateTimeOffset Due = new(2026, 11, 18, 15, 17, 0, TimeSpan.Zero);

    [Fact]
    public void Tolerancia_Termina_Exatamente_Depois_De_48_Horas()
    {
        var boundary = Due.AddHours(48);
        Assert.Equal(boundary, DelinquencyPolicy.WriteBlockStartsAt(Due));
        Assert.True(DelinquencyPolicy.CanWrite(DelinquencyPolicy.Evaluate(Due, boundary.AddTicks(-1))));
        Assert.False(DelinquencyPolicy.CanWrite(DelinquencyPolicy.Evaluate(Due, boundary)));
        Assert.Equal(DelinquencyState.WriteBlocked, DelinquencyPolicy.Evaluate(Due, Due.AddDays(60)));
    }

    [Fact]
    public void Instante_Anterior_A_Falha_Eh_Invalido() =>
        Assert.Throws<DomainRuleException>(() => DelinquencyPolicy.Evaluate(Due, Due.AddTicks(-1)));
}
