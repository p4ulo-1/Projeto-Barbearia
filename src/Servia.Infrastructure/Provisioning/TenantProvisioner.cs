using Microsoft.EntityFrameworkCore;
using Servia.Domain.Common;
using Servia.Domain.Identity;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;

namespace Servia.Infrastructure.Provisioning;

/// <summary>
/// Provisiona um novo estabelecimento (F1.4).
///
/// Cria o tenant, a assinatura em trial e o usuário proprietário em uma única
/// operação. O tenant é criado <b>do zero</b>, com dados próprios — não há
/// reaproveitamento de dados de outro estabelecimento (ADR-001).
///
/// A operação é atômica: se qualquer etapa falhar, nada é gravado.
/// </summary>
public sealed class TenantProvisioner(AppDbContext context, TimeProvider timeProvider)
{
    /// <summary>
    /// Provisiona um estabelecimento com assinatura em trial e proprietário.
    /// </summary>
    /// <returns>O tenant criado e o usuário proprietário.</returns>
    public async Task<ProvisionedTenant> ProvisionAsync(
        ProvisionTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var plan = await context.Plans
            .FirstOrDefaultAsync(item => item.Code == request.PlanCode && item.IsActive, cancellationToken);

        if (plan is null)
        {
            throw new DomainRuleException($"Plano '{request.PlanCode}' não encontrado ou inativo.");
        }

        var slugTaken = await context.Tenants
            .AnyAsync(item => item.Slug == request.Slug.Trim().ToLowerInvariant(), cancellationToken);

        if (slugTaken)
        {
            throw new DomainRuleException($"O identificador '{request.Slug}' já está em uso.");
        }

        var tenant = Tenant.Create(
            request.Slug,
            request.Name,
            request.Segment,
            request.Document,
            now);

        var subscription = Subscription.StartTrial(tenant.Id, plan.Id, today);

        var owner = User.CreateForTenant(
            tenant.Id,
            request.OwnerName,
            request.OwnerEmail,
            request.OwnerPasswordHash,
            Role.Owner,
            now);

        // Uma única gravação: ou tudo entra, ou nada entra.
        context.Tenants.Add(tenant);
        context.Subscriptions.Add(subscription);
        context.Users.Add(owner);

        await context.SaveChangesAsync(cancellationToken);

        return new ProvisionedTenant(tenant, subscription, owner);
    }
}

/// <summary>Dados necessários para provisionar um estabelecimento.</summary>
public sealed record ProvisionTenantRequest(
    string Slug,
    string Name,
    SegmentType Segment,
    string Document,
    string PlanCode,
    string OwnerName,
    string OwnerEmail,
    string OwnerPasswordHash);

/// <summary>Resultado do provisionamento.</summary>
public sealed record ProvisionedTenant(Tenant Tenant, Subscription Subscription, User Owner);
