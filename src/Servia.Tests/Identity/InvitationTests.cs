using Servia.Domain.Common;
using Servia.Domain.Identity;

namespace Servia.Tests.Identity;

/// <summary>
/// F1.7 — convite de membros da equipe, com aceite e papel definido.
/// </summary>
public class InvitationTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private static Invitation NewInvitation(Role role = Role.Professional) =>
        Invitation.Issue(TenantId, "novo@exemplo.com", role, "hash-do-convite", Now);

    // ---------- Emissao ----------

    [Fact]
    public void Emitir_Convite_Define_Validade_E_Papel()
    {
        var invitation = NewInvitation(Role.Receptionist);

        Assert.Equal(TenantId, invitation.TenantId);
        Assert.Equal("novo@exemplo.com", invitation.Email);
        Assert.Equal(Role.Receptionist, invitation.Role);
        Assert.Equal(Now.AddDays(Invitation.DefaultLifetimeDays), invitation.ExpiresAt);
        Assert.False(invitation.IsAccepted);
    }

    [Fact]
    public void Email_Do_Convite_Eh_Normalizado()
    {
        var invitation = Invitation.Issue(TenantId, "NOVO@Exemplo.COM", Role.Professional, "hash", Now);

        Assert.Equal("novo@exemplo.com", invitation.Email);
    }

    [Fact]
    public void Emitir_Convite_Sem_Tenant_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            Invitation.Issue(Guid.Empty, "novo@exemplo.com", Role.Professional, "hash", Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("sem-arroba")]
    public void Emitir_Convite_Com_Email_Invalido_Lanca_Excecao(string email)
    {
        Assert.Throws<DomainRuleException>(() =>
            Invitation.Issue(TenantId, email, Role.Professional, "hash", Now));
    }

    [Fact]
    public void Emitir_Convite_Sem_Hash_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            Invitation.Issue(TenantId, "novo@exemplo.com", Role.Professional, "", Now));
    }

    [Fact]
    public void Emitir_Convite_Com_Validade_Invalida_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            Invitation.Issue(TenantId, "novo@exemplo.com", Role.Professional, "hash", Now, 0));
    }

    // ---------- F1.13: papeis da plataforma ----------

    [Fact]
    public void Convite_Nao_Pode_Conceder_Papel_Da_Plataforma()
    {
        Assert.Throws<DomainRuleException>(() =>
            Invitation.Issue(TenantId, "novo@exemplo.com", Role.PlatformAdmin, "hash", Now));
    }

    // ---------- Validade ----------

    [Fact]
    public void Convite_Eh_Valido_Antes_De_Expirar()
    {
        var invitation = NewInvitation();

        Assert.True(invitation.IsValid(Now));
        Assert.True(invitation.IsValid(Now.AddDays(6)));
    }

    [Fact]
    public void Convite_Expira_No_Prazo()
    {
        var invitation = NewInvitation();

        Assert.False(invitation.IsValid(Now.AddDays(Invitation.DefaultLifetimeDays)));
    }

    // ---------- Aceite ----------

    [Fact]
    public void Aceitar_Convite_Vincula_Usuario()
    {
        var invitation = NewInvitation();
        var userId = Guid.NewGuid();

        invitation.Accept(userId, Now.AddDays(1));

        Assert.True(invitation.IsAccepted);
        Assert.Equal(userId, invitation.AcceptedByUserId);
        Assert.Equal(Now.AddDays(1), invitation.AcceptedAt);
    }

    [Fact]
    public void Convite_Aceito_Nao_Eh_Mais_Valido()
    {
        var invitation = NewInvitation();
        invitation.Accept(Guid.NewGuid(), Now.AddDays(1));

        Assert.False(invitation.IsValid(Now.AddDays(2)));
    }

    [Fact]
    public void Aceitar_Convite_Duas_Vezes_Lanca_Excecao()
    {
        var invitation = NewInvitation();
        invitation.Accept(Guid.NewGuid(), Now.AddDays(1));

        Assert.Throws<DomainRuleException>(() => invitation.Accept(Guid.NewGuid(), Now.AddDays(2)));
    }

    [Fact]
    public void Aceitar_Convite_Expirado_Lanca_Excecao()
    {
        var invitation = NewInvitation();

        Assert.Throws<DomainRuleException>(() =>
            invitation.Accept(Guid.NewGuid(), Now.AddDays(Invitation.DefaultLifetimeDays)));
    }

    [Fact]
    public void Aceitar_Convite_Sem_Usuario_Lanca_Excecao()
    {
        var invitation = NewInvitation();

        Assert.Throws<DomainRuleException>(() => invitation.Accept(Guid.Empty, Now.AddDays(1)));
    }
}
