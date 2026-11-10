using Servia.Domain.Common;
using Servia.Domain.Customers;

namespace Servia.Tests.Customers;

/// <summary>
/// F2.2 — preferências do cliente.
/// </summary>
public class CustomerPreferenceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();

    [Fact]
    public void Criar_Preferencia_Com_Profissional_Preferido()
    {
        var professionalId = Guid.NewGuid();

        var preference = CustomerPreference.Create(
            TenantId, CustomerId, professionalId, "Prefere atendimento pela manhã");

        Assert.Equal(professionalId, preference.PreferredProfessionalId);
        Assert.Equal("Prefere atendimento pela manhã", preference.Notes);
    }

    [Fact]
    public void Criar_Preferencia_Sem_Profissional_Eh_Permitido()
    {
        var preference = CustomerPreference.Create(TenantId, CustomerId);

        Assert.Null(preference.PreferredProfessionalId);
        Assert.Null(preference.Notes);
    }

    [Fact]
    public void Criar_Preferencia_Sem_Cliente_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            CustomerPreference.Create(TenantId, Guid.Empty));
    }

    [Fact]
    public void Criar_Preferencia_Sem_Tenant_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            CustomerPreference.Create(Guid.Empty, CustomerId));
    }

    [Fact]
    public void Alterar_Profissional_Preferido()
    {
        var preference = CustomerPreference.Create(TenantId, CustomerId);
        var professionalId = Guid.NewGuid();

        preference.SetPreferredProfessional(professionalId);

        Assert.Equal(professionalId, preference.PreferredProfessionalId);
    }

    [Fact]
    public void Remover_Profissional_Preferido()
    {
        var preference = CustomerPreference.Create(TenantId, CustomerId, Guid.NewGuid());

        preference.SetPreferredProfessional(null);

        Assert.Null(preference.PreferredProfessionalId);
    }

    [Fact]
    public void Atualizar_Observacoes_Internas()
    {
        var preference = CustomerPreference.Create(TenantId, CustomerId);

        preference.UpdateNotes("Alérgico a produto X");

        Assert.Equal("Alérgico a produto X", preference.Notes);
    }

    [Fact]
    public void Limpar_Observacoes_Com_Texto_Vazio()
    {
        var preference = CustomerPreference.Create(TenantId, CustomerId, notes: "Nota");

        preference.UpdateNotes("   ");

        Assert.Null(preference.Notes);
    }
}
