using Microsoft.EntityFrameworkCore;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;

namespace Servia.Infrastructure.Repositories;

/// <summary>
/// Implementação EF Core de <see cref="ISubscriptionRepository"/> (F16).
///
/// O filtro global restringe as consultas ao tenant corrente; o parâmetro
/// explícito documenta a intenção e protege caso o filtro seja desativado.
/// </summary>
public sealed class SubscriptionRepository(AppDbContext context) : ISubscriptionRepository
{
    public async Task<Subscription?> FindCurrentAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default) =>
        await context.Subscriptions
            .Where(subscription => subscription.TenantId == tenantId)
            .OrderByDescending(subscription => subscription.CurrentPeriodStart)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(
        Subscription subscription,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        await context.Subscriptions.AddAsync(subscription, cancellationToken);
    }

    public async Task AddChangeAsync(
        PlanChange change,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);

        await context.PlanChanges.AddAsync(change, cancellationToken);
    }

    public async Task<IReadOnlyList<PlanChange>> ListChangesAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default) =>
        await context.PlanChanges
            .Where(change => change.TenantId == tenantId)
            .OrderByDescending(change => change.ChangedAt)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}

/// <summary>
/// Implementação EF Core de <see cref="IPlanRepository"/> (F16).
///
/// <c>Plan</c> é global: não há filtro de tenant.
/// </summary>
public sealed class PlanRepository(AppDbContext context) : IPlanRepository
{
    public async Task<Plan?> FindByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var normalized = code.Trim().ToUpperInvariant();

        return await context.Plans
            .FirstOrDefaultAsync(plan => plan.Code == normalized, cancellationToken);
    }

    public async Task<Plan?> FindByIdAsync(
        Guid planId,
        CancellationToken cancellationToken = default) =>
        await context.Plans
            .FirstOrDefaultAsync(plan => plan.Id == planId, cancellationToken);

    public async Task<Plan?> FindSmallestForAsync(
        int professionalCount,
        CancellationToken cancellationToken = default)
    {
        // Planos com IncludedProfessionals = 0 sao ilimitados: sempre
        // comportam. Por isso sao considerados apenas se nenhum plano
        // com faixa numerica servir.
        var plans = await context.Plans
            .Where(plan => plan.IsActive)
            .OrderBy(plan => plan.IncludedProfessionals == 0
                ? int.MaxValue
                : plan.IncludedProfessionals)
            .ToListAsync(cancellationToken);

        return plans.FirstOrDefault(plan => plan.AllowsProfessionals(professionalCount));
    }
}

/// <summary>
/// Implementação EF Core de <see cref="IProfessionalCountReader"/> (F16.4).
/// </summary>
public sealed class ProfessionalCountReader(AppDbContext context) : IProfessionalCountReader
{
    public Task<int> CountActiveAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default) =>
        context.Professionals
            .Where(professional => professional.TenantId == tenantId && professional.IsActive)
            .CountAsync(cancellationToken);
}
