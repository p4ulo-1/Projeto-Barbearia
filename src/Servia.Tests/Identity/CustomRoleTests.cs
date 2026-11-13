using Servia.Domain.Common;
using Servia.Domain.Identity;

namespace Servia.Tests.Identity;

/// <summary>
/// Seção 3: papéis personalizados.
///
/// Pertencem ao tenant, agrupam permissões escolhidas e não podem conceder
/// permissões exclusivas do Owner ou da plataforma.
/// </summary>
public class CustomRoleTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public void Criar_Papel_Com_Permissoes_Validas()
    {
        var role = CustomRole.Create(
            TenantId, "Atendente", [Permission.ViewServices, Permission.ManageAppointments]);

        Assert.Equal("Atendente", role.Name);
        Assert.Equal(TenantId, role.TenantId);
        Assert.True(role.Has(Permission.ViewServices));
        Assert.True(role.Has(Permission.ManageAppointments));
        Assert.Equal(2, role.Permissions.Count);
    }

    [Fact]
    public void Nome_Vazio_E_Rejeitado()
    {
        Assert.Throws<DomainRuleException>(() =>
            CustomRole.Create(TenantId, "  ", [Permission.ViewServices]));
    }

    [Fact]
    public void Nao_Pode_Conceder_ManageBilling()
    {
        Assert.Throws<DomainRuleException>(() =>
            CustomRole.Create(TenantId, "Gerente", [Permission.ManageBilling]));
    }

    [Fact]
    public void Nao_Pode_Conceder_Permissoes_De_Plataforma()
    {
        Assert.Throws<DomainRuleException>(() =>
            CustomRole.Create(TenantId, "Plataforma", [Permission.ManagePlatformTenants]));

        Assert.Throws<DomainRuleException>(() =>
            CustomRole.Create(TenantId, "Plataforma", [Permission.ManagePlatformPlans]));
    }

    [Fact]
    public void Renomear_E_Substituir_Permissoes()
    {
        var role = CustomRole.Create(TenantId, "Atendente", [Permission.ViewServices]);

        role.Rename("Recepcionista Sênior");
        role.ReplacePermissions([Permission.ViewTeamAgenda, Permission.ManageCustomers]);

        Assert.Equal("Recepcionista Sênior", role.Name);
        Assert.False(role.Has(Permission.ViewServices));
        Assert.True(role.Has(Permission.ViewTeamAgenda));
        Assert.True(role.Has(Permission.ManageCustomers));
    }

    [Fact]
    public void Substituir_Permissoes_Rejeita_Exclusiva_Do_Owner()
    {
        var role = CustomRole.Create(TenantId, "Atendente", [Permission.ViewServices]);

        Assert.Throws<DomainRuleException>(() =>
            role.ReplacePermissions([Permission.ViewServices, Permission.ManageBilling]));

        // O conjunto anterior permanece intacto.
        Assert.True(role.Has(Permission.ViewServices));
        Assert.False(role.Has(Permission.ManageBilling));
    }

    [Fact]
    public void Permissoes_Atribuiveis_Excluem_Owner_E_Plataforma()
    {
        var assignable = RolePermissions.AssignableToCustomRole;

        Assert.DoesNotContain(Permission.ManageBilling, assignable);
        Assert.DoesNotContain(Permission.ManagePlatformTenants, assignable);
        Assert.DoesNotContain(Permission.ManagePlatformPlans, assignable);

        Assert.Contains(Permission.ManageFinancialEntries, assignable);
        Assert.Contains(Permission.ManageAppointments, assignable);
    }

    // ---------- Prevenção de escalonamento (seção 1.2) ----------

    [Fact]
    public void Ator_Nao_Concede_Permissao_Que_Nao_Possui()
    {
        // Administrator tem ManageTeam, mas não ManageBilling.
        var actor = RolePermissions.For(Role.Administrator);

        Assert.Throws<DomainRuleException>(() =>
            RolePermissions.EnsureCanGrant(actor, [Permission.ManageBilling]));
    }

    [Fact]
    public void Ator_Concede_Apenas_O_Que_Possui()
    {
        var actor = RolePermissions.For(Role.Administrator);

        // Não lança: todas as permissões estão no conjunto do ator.
        RolePermissions.EnsureCanGrant(
            actor, [Permission.ViewServices, Permission.ManageAppointments]);
    }

    [Fact]
    public void Owner_Pode_Conceder_Qualquer_Permissao_Atribuivel()
    {
        var owner = RolePermissions.For(Role.Owner);

        RolePermissions.EnsureCanGrant(owner, RolePermissions.AssignableToCustomRole);
    }

    [Fact]
    public void Ator_Sem_Permissao_Nao_Concede_Nada()
    {
        var actor = RolePermissions.For(Role.Professional);

        Assert.Throws<DomainRuleException>(() =>
            RolePermissions.EnsureCanGrant(actor, [Permission.ManageTeam]));
    }
}
