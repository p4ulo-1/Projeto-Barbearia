using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Loyalty;

/// <summary>
/// Campanha de recuperação de clientes inativos (F7.6).
///
/// Identifica clientes sem visita há N dias e dispara comunicação. A
/// campanha respeita o consentimento: cliente que não autorizou comunicação
/// não recebe mensagem, mesmo estando inativo.
///
/// O alvo é calculado por uma função pura: dado o último atendimento e a
/// data atual, o cliente está ou não inativo.
/// </summary>
public sealed class ReactivationCampaign : ITenantScoped
{
    private ReactivationCampaign(
        Guid id,
        Guid tenantId,
        string name,
        int inactiveDays,
        bool isActive)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        InactiveDays = inactiveDays;
        IsActive = isActive;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>.
    /// </summary>
    private ReactivationCampaign()
    {
        Name = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; }

    /// <summary>Dias sem visita que caracterizam inatividade.</summary>
    public int InactiveDays { get; private set; }

    public bool IsActive { get; private set; }

    public static ReactivationCampaign Create(Guid tenantId, string name, int inactiveDays)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainRuleException("O nome da campanha é obrigatório.");
        }

        if (inactiveDays <= 0)
        {
            throw new DomainRuleException("Os dias de inatividade devem ser maiores que zero.");
        }

        return new ReactivationCampaign(
            Guid.NewGuid(), tenantId, name.Trim(), inactiveDays, isActive: true);
    }

    /// <summary>
    /// Indica se um cliente está inativo, dado o último atendimento.
    /// Cliente que nunca veio é considerado inativo.
    /// </summary>
    public bool IsInactive(DateOnly? lastVisitDate, DateOnly today)
    {
        if (lastVisitDate is null)
        {
            return true;
        }

        return today.DayNumber - lastVisitDate.Value.DayNumber >= InactiveDays;
    }

    /// <summary>
    /// Indica se o cliente deve receber a campanha.
    /// Exige inatividade <b>e</b> consentimento para comunicação.
    /// </summary>
    public bool ShouldTarget(DateOnly? lastVisitDate, DateOnly today, bool hasConsent) =>
        IsActive && hasConsent && IsInactive(lastVisitDate, today);

    /// <summary>Desativa a campanha.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Reativa a campanha.</summary>
    public void Activate() => IsActive = true;
}
