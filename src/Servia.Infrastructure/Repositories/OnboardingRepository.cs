using Microsoft.EntityFrameworkCore;
using Servia.Domain.Ai;
using Servia.Domain.Common;
using Servia.Domain.Sites;
using Servia.Infrastructure.Persistence;

namespace Servia.Infrastructure.Repositories;

/// <summary>
/// Implementação EF Core de <see cref="IOnboardingRepository"/> (itens 1 e 2).
///
/// O filtro global restringe as consultas ao tenant corrente; o parâmetro
/// explícito de tenant documenta a intenção e protege caso o filtro seja
/// desativado. Toda entidade é escopada por estabelecimento.
/// </summary>
public sealed class OnboardingRepository(AppDbContext context) : IOnboardingRepository
{
    public async Task<OnboardingSession?> FindOpenSessionAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default) =>
        await context.OnboardingSessions
            .Where(session => session.TenantId == tenantId
                && (session.Status == OnboardingStatus.Collecting
                    || session.Status == OnboardingStatus.Reviewing
                    || session.Status == OnboardingStatus.AwaitingApproval
                    || session.Status == OnboardingStatus.Approved))
            .OrderByDescending(session => session.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<OnboardingSession?> FindSessionAsync(
        Guid tenantId,
        Guid sessionId,
        CancellationToken cancellationToken = default) =>
        await context.OnboardingSessions
            .Where(session => session.TenantId == tenantId && session.Id == sessionId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<OnboardingSession>> ListSessionsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default) =>
        await context.OnboardingSessions
            .Where(session => session.TenantId == tenantId)
            .OrderByDescending(session => session.StartedAt)
            .ToListAsync(cancellationToken);

    public async Task AddSessionAsync(
        OnboardingSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        await context.OnboardingSessions.AddAsync(session, cancellationToken);
    }

    public async Task<AiChangeSet?> FindChangeSetAsync(
        Guid tenantId,
        Guid changeSetId,
        CancellationToken cancellationToken = default) =>
        await context.AiChangeSets
            .Where(changeSet => changeSet.TenantId == tenantId && changeSet.Id == changeSetId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddChangeSetAsync(
        AiChangeSet changeSet,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(changeSet);

        await context.AiChangeSets.AddAsync(changeSet, cancellationToken);
    }

    public async Task<Site?> FindSiteAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default) =>
        await context.Sites
            .Where(site => site.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddSiteAsync(Site site, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(site);

        await context.Sites.AddAsync(site, cancellationToken);
    }

    public async Task AddAssetAsync(SiteAsset asset, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asset);

        await context.SiteAssets.AddAsync(asset, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            // C10: outra transacao alterou o agregado (ex.: publicacao
            // concorrente do mesmo site). Traduz para o dominio, sem expor EF.
            throw new ConcurrencyConflictException(
                "O site foi alterado por outra operação. Recarregue e tente novamente.",
                exception);
        }
    }
}
