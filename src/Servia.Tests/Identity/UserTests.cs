using Servia.Domain.Common;
using Servia.Domain.Identity;

namespace Servia.Tests.Identity;

/// <summary>
/// F1.5 — usuário por tenant, com papel e permissões.
/// F1.9 — identidade independente por tenant, sem vínculo global.
/// F1.13 — separação entre usuário de plataforma e de estabelecimento.
/// </summary>
public class UserTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private static User NewOwner() =>
        User.CreateForTenant(TenantId, "Ana Souza", "ana@exemplo.com", "hash", Role.Owner, Now);

    // ---------- Criacao ----------

    [Fact]
    public void Criar_Usuario_De_Tenant_Inicia_Ativo()
    {
        var user = NewOwner();

        Assert.Equal(TenantId, user.TenantId);
        Assert.Equal(Role.Owner, user.Role);
        Assert.True(user.IsActive);
        Assert.False(user.IsPlatformAdmin);
        Assert.Equal(Now, user.CreatedAt);
    }

    [Fact]
    public void Email_Eh_Normalizado_Para_Minusculas()
    {
        var user = User.CreateForTenant(TenantId, "Ana", "ANA@Exemplo.COM", "hash", Role.Owner, Now);

        Assert.Equal("ana@exemplo.com", user.Email);
    }

    [Fact]
    public void Criar_Usuario_Sem_Tenant_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            User.CreateForTenant(Guid.Empty, "Ana", "ana@exemplo.com", "hash", Role.Owner, Now));
    }

    [Fact]
    public void Criar_Usuario_Sem_Nome_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            User.CreateForTenant(TenantId, "  ", "ana@exemplo.com", "hash", Role.Owner, Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("sem-arroba")]
    public void Criar_Usuario_Com_Email_Invalido_Lanca_Excecao(string email)
    {
        Assert.Throws<DomainRuleException>(() =>
            User.CreateForTenant(TenantId, "Ana", email, "hash", Role.Owner, Now));
    }

    [Fact]
    public void Criar_Usuario_Sem_Senha_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            User.CreateForTenant(TenantId, "Ana", "ana@exemplo.com", "", Role.Owner, Now));
    }

    // ---------- F1.9: identidade independente por tenant ----------

    [Fact]
    public void Mesma_Pessoa_Em_Dois_Tenants_Gera_Registros_Independentes()
    {
        var otherTenant = Guid.NewGuid();

        var inTenantA = User.CreateForTenant(TenantId, "João", "joao@exemplo.com", "hash", Role.Customer, Now);
        var inTenantB = User.CreateForTenant(otherTenant, "João", "joao@exemplo.com", "hash", Role.Customer, Now);

        // Mesmo e-mail, registros distintos, sem vínculo.
        Assert.NotEqual(inTenantA.Id, inTenantB.Id);
        Assert.NotEqual(inTenantA.TenantId, inTenantB.TenantId);
        Assert.Equal(inTenantA.Email, inTenantB.Email);
    }

    [Fact]
    public void Mesma_Pessoa_Pode_Ter_Papeis_Diferentes_Por_Tenant()
    {
        var otherTenant = Guid.NewGuid();

        var owner = User.CreateForTenant(TenantId, "João", "joao@exemplo.com", "hash", Role.Owner, Now);
        var customer = User.CreateForTenant(otherTenant, "João", "joao@exemplo.com", "hash", Role.Customer, Now);

        Assert.Equal(Role.Owner, owner.Role);
        Assert.Equal(Role.Customer, customer.Role);
    }

    // ---------- F1.13: plataforma vs tenant ----------

    [Fact]
    public void Criar_PlatformAdmin_Nao_Pertence_A_Tenant()
    {
        var admin = User.CreatePlatformAdmin("Suporte", "suporte@servia.com", "hash", Now);

        Assert.True(admin.IsPlatformAdmin);
        Assert.Equal(Guid.Empty, admin.TenantId);
    }

    [Fact]
    public void Usuario_De_Tenant_Nao_Pode_Ser_PlatformAdmin()
    {
        Assert.Throws<DomainRuleException>(() =>
            User.CreateForTenant(TenantId, "Ana", "ana@exemplo.com", "hash", Role.PlatformAdmin, Now));
    }

    [Fact]
    public void Nao_E_Possivel_Converter_Tenant_Em_Plataforma()
    {
        var user = NewOwner();

        Assert.Throws<DomainRuleException>(() => user.ChangeRole(Role.PlatformAdmin));
        Assert.Equal(Role.Owner, user.Role);
    }

    [Fact]
    public void Nao_E_Possivel_Converter_Plataforma_Em_Tenant()
    {
        var admin = User.CreatePlatformAdmin("Suporte", "suporte@servia.com", "hash", Now);

        Assert.Throws<DomainRuleException>(() => admin.ChangeRole(Role.Owner));
    }

    // ---------- Permissoes ----------

    [Fact]
    public void Usuario_Herda_Permissoes_Do_Papel()
    {
        var owner = NewOwner();

        Assert.True(owner.HasPermission(Permission.ManageTenantSettings));
        Assert.False(owner.HasPermission(Permission.ManagePlatformTenants));
    }

    [Fact]
    public void Usuario_Inativo_Nao_Possui_Permissoes()
    {
        var owner = NewOwner();
        owner.Deactivate();

        Assert.False(owner.HasPermission(Permission.ManageTenantSettings));
    }

    [Fact]
    public void Alterar_Papel_Altera_Permissoes()
    {
        var user = NewOwner();

        user.ChangeRole(Role.Customer);

        Assert.False(user.HasPermission(Permission.ManageTenantSettings));
        Assert.True(user.HasPermission(Permission.ViewServices));
    }

    // ---------- Sessao ----------

    [Fact]
    public void Registrar_Login_Atualiza_Instante()
    {
        var user = NewOwner();

        user.RegisterLogin(Now);

        Assert.Equal(Now, user.LastLoginAt);
    }

    [Fact]
    public void Usuario_Inativo_Nao_Pode_Autenticar()
    {
        var user = NewOwner();
        user.Deactivate();

        Assert.False(user.CanSignIn);
        Assert.Throws<DomainRuleException>(() => user.RegisterLogin(Now));
    }

    [Fact]
    public void Desativar_E_Reativar_Alteram_Estado()
    {
        var user = NewOwner();

        user.Deactivate();
        Assert.False(user.CanSignIn);

        user.Activate();
        Assert.True(user.CanSignIn);
    }
}
