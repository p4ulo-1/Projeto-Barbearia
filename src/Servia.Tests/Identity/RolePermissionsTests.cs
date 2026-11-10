using Servia.Domain.Identity;

namespace Servia.Tests.Identity;

/// <summary>
/// F1.5 — papéis por tenant e suas permissões.
/// F1.13 — separação de privilégios entre plataforma e estabelecimento.
/// </summary>
public class RolePermissionsTests
{
    private static readonly Role[] TenantRoles =
    [
        Role.Owner, Role.Administrator, Role.Professional, Role.Receptionist, Role.Customer
    ];

    // ---------- F1.5: permissoes por papel ----------

    [Fact]
    public void Owner_Possui_Todas_As_Permissoes_De_Tenant()
    {
        var owner = RolePermissions.For(Role.Owner);

        Assert.Contains(Permission.ManageTenantSettings, owner);
        Assert.Contains(Permission.ManageTeam, owner);
        Assert.Contains(Permission.ManageBilling, owner);
        Assert.Contains(Permission.ManageFinancialEntries, owner);
    }

    [Fact]
    public void Administrator_Nao_Gere_Billing()
    {
        Assert.False(RolePermissions.Has(Role.Administrator, Permission.ManageBilling));
        Assert.True(RolePermissions.Has(Role.Administrator, Permission.ManageTeam));
    }

    [Fact]
    public void Professional_Ve_Apenas_A_Propria_Agenda()
    {
        Assert.True(RolePermissions.Has(Role.Professional, Permission.ViewOwnAgenda));
        Assert.False(RolePermissions.Has(Role.Professional, Permission.ViewTeamAgenda));
    }

    [Fact]
    public void Professional_Ve_Apenas_A_Propria_Comissao()
    {
        Assert.True(RolePermissions.Has(Role.Professional, Permission.ViewOwnCommissions));
        Assert.False(RolePermissions.Has(Role.Professional, Permission.ViewFinancialReports));
    }

    [Fact]
    public void Receptionist_Gere_Agenda_Mas_Nao_Financas()
    {
        Assert.True(RolePermissions.Has(Role.Receptionist, Permission.ManageAppointments));
        Assert.True(RolePermissions.Has(Role.Receptionist, Permission.ManageCustomers));
        Assert.False(RolePermissions.Has(Role.Receptionist, Permission.ViewFinancialReports));
    }

    [Fact]
    public void Customer_Tem_Apenas_Visao_De_Servicos()
    {
        var permissions = RolePermissions.For(Role.Customer);

        Assert.Single(permissions);
        Assert.Contains(Permission.ViewServices, permissions);
    }

    [Fact]
    public void Cada_Papel_De_Tenant_Tem_Permissoes()
    {
        Assert.All(TenantRoles, role =>
            Assert.NotEmpty(RolePermissions.For(role)));
    }

    // ---------- F1.13: separacao de privilegios ----------

    [Fact]
    public void PlatformAdmin_Nao_Possui_Permissoes_De_Tenant()
    {
        var platformPermissions = RolePermissions.For(Role.PlatformAdmin);

        Assert.DoesNotContain(Permission.ManageTenantSettings, platformPermissions);
        Assert.DoesNotContain(Permission.ManageAppointments, platformPermissions);
        Assert.DoesNotContain(Permission.ViewFinancialReports, platformPermissions);
    }

    [Fact]
    public void Nenhum_Papel_De_Tenant_Possui_Permissoes_De_Plataforma()
    {
        Assert.All(TenantRoles, role =>
        {
            Assert.False(RolePermissions.Has(role, Permission.ManagePlatformTenants));
            Assert.False(RolePermissions.Has(role, Permission.ManagePlatformPlans));
        });
    }

    [Fact]
    public void PlatformAdmin_Possui_Apenas_Permissoes_De_Plataforma()
    {
        var permissions = RolePermissions.For(Role.PlatformAdmin);

        Assert.All(permissions, permission =>
            Assert.True(permission is Permission.ManagePlatformTenants or Permission.ManagePlatformPlans));
    }

    [Fact]
    public void Classificacao_De_Papel_Distingue_Plataforma_De_Tenant()
    {
        Assert.True(RolePermissions.IsPlatformRole(Role.PlatformAdmin));
        Assert.False(RolePermissions.IsTenantRole(Role.PlatformAdmin));

        Assert.All(TenantRoles, role =>
        {
            Assert.True(RolePermissions.IsTenantRole(role));
            Assert.False(RolePermissions.IsPlatformRole(role));
        });
    }
}
