using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Application.Billing;

/// <summary>
/// Casos de uso de assinatura (F16).
///
/// Concentra contratar, trocar e cancelar plano. A lógica vive aqui, não
/// no controller: pode ser testada sem subir a API.
///
/// <b>Regra de faixa (F16.4):</b> o plano define quantos profissionais o
/// tenant pode ter. Ao ultrapassar o limite, o tenant precisa subir de
/// categoria — não há cobrança por excedente.
/// </summary>
public sealed class SubscriptionService(
    ISubscriptionRepository subscriptions,
    IPlanRepository plans,
    IProfessionalCountReader professionalCounts,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Contrata um plano para o tenant (F16.1).
    /// </summary>
    public async Task<SubscriptionResult> SubscribeAsync(
        Guid tenantId,
        string planCode,
        Guid requestedBy,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            return SubscriptionResult.Invalid("Estabelecimento não identificado.");
        }

        var plan = await plans.FindByCodeAsync(planCode, cancellationToken);

        if (plan is null || !plan.IsActive)
        {
            return SubscriptionResult.Invalid($"Plano '{planCode}' não encontrado ou inativo.");
        }

        var existing = await subscriptions.FindCurrentAsync(tenantId, cancellationToken);

        if (existing is not null)
        {
            return SubscriptionResult.Invalid(
                "O estabelecimento já possui assinatura. Use a troca de plano.");
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var subscription = Subscription.StartTrial(tenantId, plan.Id, today);

        await subscriptions.AddAsync(subscription, cancellationToken);

        var change = PlanChange.Record(
            tenantId, subscription.Id, null, plan.Id,
            PlanChangeReason.Initial, requestedBy, timeProvider.GetUtcNow());

        await subscriptions.AddChangeAsync(change, cancellationToken);
        await subscriptions.SaveChangesAsync(cancellationToken);

        return SubscriptionResult.Succeeded(subscription, plan, change);
    }

    /// <summary>
    /// Troca o plano da assinatura (F16.2).
    ///
    /// O motivo é derivado comparando as faixas: subir é upgrade, descer é
    /// downgrade. A regra de faixa é verificada antes de aceitar.
    /// </summary>
    public async Task<SubscriptionResult> ChangePlanAsync(
        Guid tenantId,
        string planCode,
        Guid requestedBy,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            return SubscriptionResult.Invalid("Estabelecimento não identificado.");
        }

        var subscription = await subscriptions.FindCurrentAsync(tenantId, cancellationToken);

        if (subscription is null)
        {
            return SubscriptionResult.Invalid(
                "O estabelecimento não possui assinatura. Contrate um plano primeiro.");
        }

        var newPlan = await plans.FindByCodeAsync(planCode, cancellationToken);

        if (newPlan is null || !newPlan.IsActive)
        {
            return SubscriptionResult.Invalid($"Plano '{planCode}' não encontrado ou inativo.");
        }

        if (newPlan.Id == subscription.PlanId)
        {
            return SubscriptionResult.Invalid("O plano escolhido é o atual.");
        }

        var currentPlan = await plans.FindByIdAsync(subscription.PlanId, cancellationToken);

        if (currentPlan is null)
        {
            return SubscriptionResult.Invalid("O plano atual da assinatura não foi encontrado.");
        }

        // Regra de faixa (F16.4): o plano de destino precisa comportar a
        // equipe atual. Descer para um plano menor que a equipe é rejeitado.
        var professionalCount = await professionalCounts.CountActiveAsync(
            tenantId, cancellationToken);

        if (!newPlan.AllowsProfessionals(professionalCount))
        {
            return SubscriptionResult.Invalid(
                $"O plano {newPlan.Name} comporta até {newPlan.IncludedProfessionals} profissionais "
                + $"e o estabelecimento tem {professionalCount}. "
                + "Reduza a equipe ou escolha um plano maior.");
        }

        var reason = newPlan.IncludedProfessionals == 0
                     || newPlan.IncludedProfessionals > currentPlan.IncludedProfessionals
            ? PlanChangeReason.Upgrade
            : PlanChangeReason.Downgrade;

        try
        {
            subscription.ChangePlan(newPlan.Id);
        }
        catch (DomainRuleException exception)
        {
            // Regra de dominio violada: devolve erro de negocio, nao 500.
            return SubscriptionResult.Invalid(exception.Message);
        }

        var change = PlanChange.Record(
            tenantId, subscription.Id, currentPlan.Id, newPlan.Id,
            reason, requestedBy, timeProvider.GetUtcNow());

        await subscriptions.AddChangeAsync(change, cancellationToken);
        await subscriptions.SaveChangesAsync(cancellationToken);

        return SubscriptionResult.Succeeded(subscription, newPlan, change);
    }

    /// <summary>
    /// Cancela a assinatura (F16.3).
    /// O acesso permanece até o fim do período pago.
    /// </summary>
    public async Task<SubscriptionResult> CancelAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await subscriptions.FindCurrentAsync(tenantId, cancellationToken);

        if (subscription is null)
        {
            return SubscriptionResult.Invalid("O estabelecimento não possui assinatura.");
        }

        try
        {
            subscription.Cancel();
        }
        catch (DomainRuleException exception)
        {
            return SubscriptionResult.Invalid(exception.Message);
        }

        await subscriptions.SaveChangesAsync(cancellationToken);

        var plan = await plans.FindByIdAsync(subscription.PlanId, cancellationToken);

        return SubscriptionResult.Succeeded(subscription, plan, null);
    }

    /// <summary>
    /// Verifica se a equipe atual excede a faixa do plano e, se sim,
    /// indica o menor plano que a comporta (F16.4).
    /// </summary>
    public async Task<PlanUpgradeSuggestion?> SuggestUpgradeAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await subscriptions.FindCurrentAsync(tenantId, cancellationToken);

        if (subscription is null)
        {
            return null;
        }

        var currentPlan = await plans.FindByIdAsync(subscription.PlanId, cancellationToken);

        if (currentPlan is null)
        {
            return null;
        }

        var professionalCount = await professionalCounts.CountActiveAsync(
            tenantId, cancellationToken);

        if (currentPlan.AllowsProfessionals(professionalCount))
        {
            return null;
        }

        var required = await plans.FindSmallestForAsync(professionalCount, cancellationToken);

        return required is null
            ? null
            : new PlanUpgradeSuggestion(currentPlan, required, professionalCount);
    }
}

/// <summary>Resultado de uma operação de assinatura.</summary>
public sealed record SubscriptionResult(
    bool Success,
    Subscription? Subscription,
    Plan? Plan,
    PlanChange? Change,
    string? FailureReason)
{
    public static SubscriptionResult Succeeded(
        Subscription subscription, Plan? plan, PlanChange? change) =>
        new(true, subscription, plan, change, null);

    public static SubscriptionResult Invalid(string reason) =>
        new(false, null, null, null, reason);
}

/// <summary>Sugestão de upgrade por excedente de profissionais.</summary>
public sealed record PlanUpgradeSuggestion(
    Plan CurrentPlan,
    Plan RequiredPlan,
    int ProfessionalCount);
