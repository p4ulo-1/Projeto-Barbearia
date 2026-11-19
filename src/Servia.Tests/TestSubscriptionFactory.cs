using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Tests;

/// <summary>Cria dados de teste por meio das transições de autorização atuais.</summary>
internal static class TestSubscriptionFactory
{
    public static Subscription AuthorizedTrial(Guid tenantId, Guid planId, DateOnly day, string? customerId = null)
    {
        if (tenantId == Guid.Empty || planId == Guid.Empty) throw new DomainRuleException("Tenant e plano são obrigatórios.");
        var plan = Plan.Create("TEST", "Teste", Money.From(99m), 0, SiteCustomizationLevel.Basic, 0);
        typeof(Plan).GetProperty(nameof(Plan.Id))!.SetValue(plan, planId);
        var trial = Subscription.StartCheckout(tenantId, plan, Guid.NewGuid(), day);
        trial.UseAuthorizedTrial("trial-plan-test", planId);
        var start = Subscription.AtUtc(day);
        trial.MarkCheckoutDispatched(start, "test@example.invalid");
        trial.BindCheckout($"test-{trial.Id:N}");
        trial.ConfirmAuthorization(trial.GatewaySubscriptionId!);
        trial.BeginAuthorizedTrial(trial.GatewaySubscriptionId!, start, start.AddDays(Subscription.TrialDays));
        if (customerId is not null) typeof(Subscription).GetProperty(nameof(Subscription.GatewayCustomerId))!.SetValue(trial, customerId);
        return trial;
    }
}
