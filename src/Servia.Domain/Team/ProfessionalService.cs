using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Team;

/// <summary>
/// Vínculo entre profissional e serviço.
///
/// Define quais serviços um profissional executa. A disponibilidade respeita
/// esse vínculo: um horário só é oferecido se o profissional executa o
/// serviço solicitado.
/// </summary>
public sealed class ProfessionalService : ITenantScoped
{
    private ProfessionalService(Guid tenantId, Guid professionalId, Guid serviceId)
    {
        TenantId = tenantId;
        ProfessionalId = professionalId;
        ServiceId = serviceId;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>.
    /// </summary>
    private ProfessionalService()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid ProfessionalId { get; private set; }

    public Guid ServiceId { get; private set; }

    public static ProfessionalService Create(Guid tenantId, Guid professionalId, Guid serviceId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (professionalId == Guid.Empty)
        {
            throw new DomainRuleException("O profissional é obrigatório.");
        }

        if (serviceId == Guid.Empty)
        {
            throw new DomainRuleException("O serviço é obrigatório.");
        }

        return new ProfessionalService(tenantId, professionalId, serviceId);
    }
}
