using Microsoft.EntityFrameworkCore;
using Servia.Domain.Team;
using Servia.Infrastructure.Persistence;

namespace Servia.Infrastructure.Repositories;

public static class EligibleProfessionalReader
{
    public static async Task<IReadOnlyList<(Professional Professional, List<Guid> ServiceIds)>> ListAsync(
        AppDbContext context,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var candidates = await (
            from professional in context.Professionals.AsNoTracking()
            join link in context.ProfessionalServices
                on new { professional.TenantId, ProfessionalId = professional.Id }
                equals new { link.TenantId, link.ProfessionalId }
            join service in context.Services
                on new { link.TenantId, link.ServiceId }
                equals new { service.TenantId, ServiceId = service.Id }
            where professional.TenantId == tenantId
            orderby professional.Name
            select new { Professional = professional, Service = service })
            .ToListAsync(cancellationToken);

        return candidates
            .Where(candidate => BookingEligibility.CanExecute(
                candidate.Professional, candidate.Service, isLinked: true))
            .GroupBy(candidate => candidate.Professional.Id)
            .Select(group => (group.First().Professional, group.Select(item => item.Service.Id).ToList()))
            .ToList();
    }
}
