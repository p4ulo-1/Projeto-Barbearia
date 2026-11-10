using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Customers;

/// <summary>
/// Preferências de um cliente dentro do estabelecimento.
///
/// Registra o profissional preferido e observações internas que ajudam no
/// atendimento. É parte do relacionamento comercial com o tenant, não da
/// identidade de acesso.
/// </summary>
public sealed class CustomerPreference : ITenantScoped
{
    private CustomerPreference(
        Guid tenantId,
        Guid customerId,
        Guid? preferredProfessionalId,
        string? notes)
    {
        TenantId = tenantId;
        CustomerId = customerId;
        PreferredProfessionalId = preferredProfessionalId;
        Notes = notes;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>.
    /// </summary>
    private CustomerPreference()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid? PreferredProfessionalId { get; private set; }

    /// <summary>Observações internas, visíveis apenas para a equipe.</summary>
    public string? Notes { get; private set; }

    public static CustomerPreference Create(
        Guid tenantId,
        Guid customerId,
        Guid? preferredProfessionalId = null,
        string? notes = null)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (customerId == Guid.Empty)
        {
            throw new DomainRuleException("O cliente é obrigatório.");
        }

        return new CustomerPreference(
            tenantId,
            customerId,
            preferredProfessionalId,
            string.IsNullOrWhiteSpace(notes) ? null : notes.Trim());
    }

    /// <summary>Define o profissional preferido.</summary>
    public void SetPreferredProfessional(Guid? professionalId) =>
        PreferredProfessionalId = professionalId;

    /// <summary>Atualiza as observações internas.</summary>
    public void UpdateNotes(string? notes) =>
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
}
