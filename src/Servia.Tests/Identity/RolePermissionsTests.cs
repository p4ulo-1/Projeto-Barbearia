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
    public void Administrator_Gere_Financas_Internas_Mas_Nao_Billing()
    {
        // Seção 1: o administrador administra o financeiro interno, mas a
        // relação comercial com a plataforma (plano) é exclusiva do Owner.
        Assert.False(RolePermissions.Has(Role.Administrator, Permission.ManageBilling));
        Assert.True(RolePermissions.Has(Role.Administrator, Permission.ManageFinancialEntries));
        Assert.True(RolePermissions.Has(Role.Administrator, Permission.ViewFinancialReports));
        Assert.True(RolePermissions.Has(Role.Administrator, Permission.ManageTeam));
    }

    [Fact]
    public void Owner_Tem_Todas_As_Permissoes_Do_Administrator()
    {
        // Seção 1: o Owner possui todas as permissões do Administrator,
        // além das exclusivas de titularidade.
        Assert.All(RolePermissions.For(Role.Administrator), permission =>
            Assert.True(RolePermissions.Has(Role.Owner, permission),
                $"Owner deveria ter {permission}."));

        Assert.True(RolePermissions.Has(Role.Owner, Permission.ManageBilling));
    }

    [Fact]
    public void ManageBilling_E_Exclusiva_Do_Owner()
    {
        Assert.True(RolePermissions.OwnerExclusivePermissions.Contains(Permission.ManageBilling));

        Assert.All(TenantRoles.Where(role => role != Role.Owner), role =>
            Assert.False(RolePermissions.Has(role, Permission.ManageBilling),
                $"{role} não deveria ter ManageBilling."));
    }

    [Fact]
    public void Administrator_Tem_Todas_As_Permissoes_De_Tenant_Exceto_As_Exclusivas()
    {
        // O administrador tem todas as permissões de tenant, exceto as de
        // escopo "próprio" (profissional) e as exclusivas do Owner.
        var tenantPermissions = Enum.GetValues<Permission>()
            .Where(permission => permission is not (Permission.ManagePlatformTenants
                or Permission.ManagePlatformPlans
                or Permission.ViewOwnAgenda
                or Permission.ViewOwnCommissions)
                && !RolePermissions.OwnerExclusivePermissions.Contains(permission));

        Assert.All(tenantPermissions, permission =>
            Assert.True(RolePermissions.Has(Role.Administrator, permission),
                $"Administrador deveria ter {permission}."));
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
    public void Receptionist_Gere_Agenda_E_Ve_Financas_Sem_Lancar()
    {
        // Seção 1: recepcionista tem permissões operacionais superiores ao
        // profissional comum, especialmente na agenda. Vê o financeiro, mas
        // não lança pagamentos/transações.
        Assert.True(RolePermissions.Has(Role.Receptionist, Permission.ManageAppointments));
        Assert.True(RolePermissions.Has(Role.Receptionist, Permission.CancelAnyAppointment));
        Assert.True(RolePermissions.Has(Role.Receptionist, Permission.ManageCustomers));
        Assert.True(RolePermissions.Has(Role.Receptionist, Permission.ViewFinancialReports));
        Assert.False(RolePermissions.Has(Role.Receptionist, Permission.ManageFinancialEntries));
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
