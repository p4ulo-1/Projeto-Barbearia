using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Tests.Billing;

public sealed class PlanChangeProrationTests
{
    [Fact]
    public void Arredonda_Apenas_O_Valor_Final_E_Usa_As_Horas_Restantes()
    {
        var start = new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);
        var end = start.AddDays(31);
        Assert.Equal(0.51m, PlanChange.Prorate(100m, 101.01m, start, end, start.AddHours(372)));
        Assert.Equal(59.96m, PlanChange.Prorate(100m, 160m, start, end, start.AddMinutes(30)));
        Assert.Throws<DomainRuleException>(() => PlanChange.Prorate(100m, 160m, start, end, end));
    }
}
