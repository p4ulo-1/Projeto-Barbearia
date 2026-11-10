using Servia.Domain.Common;
using Servia.Domain.Identity;

namespace Servia.Tests.Identity;

/// <summary>
/// F1.6 — refresh token com revogação.
/// Verifica emissão, validade, expiração e revogação imediata.
/// </summary>
public class RefreshTokenTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private static RefreshToken NewToken(int lifetimeDays = RefreshToken.DefaultLifetimeDays) =>
        RefreshToken.Issue(TenantId, UserId, "hash-do-token", Now, lifetimeDays);

    // ---------- Emissao ----------

    [Fact]
    public void Emitir_Token_Define_Validade_Padrao()
    {
        var token = NewToken();

        Assert.Equal(UserId, token.UserId);
        Assert.Equal(TenantId, token.TenantId);
        Assert.Equal(Now.AddDays(RefreshToken.DefaultLifetimeDays), token.ExpiresAt);
        Assert.False(token.IsRevoked);
    }

    [Fact]
    public void Emitir_Token_Sem_Usuario_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            RefreshToken.Issue(TenantId, Guid.Empty, "hash", Now));
    }

    [Fact]
    public void Emitir_Token_Sem_Hash_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            RefreshToken.Issue(TenantId, UserId, "  ", Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Emitir_Token_Com_Validade_Invalida_Lanca_Excecao(int days)
    {
        Assert.Throws<DomainRuleException>(() =>
            RefreshToken.Issue(TenantId, UserId, "hash", Now, days));
    }

    // ---------- Validade ----------

    [Fact]
    public void Token_Eh_Valido_Antes_De_Expirar()
    {
        var token = NewToken();

        Assert.True(token.IsValid(Now));
        Assert.True(token.IsValid(Now.AddDays(6)));
    }

    [Fact]
    public void Token_Expira_No_Prazo()
    {
        var token = NewToken();

        Assert.False(token.IsValid(Now.AddDays(RefreshToken.DefaultLifetimeDays)));
        Assert.False(token.IsValid(Now.AddDays(30)));
    }

    // ---------- Revogacao ----------

    [Fact]
    public void Revogar_Torna_Token_Invalido_Imediatamente()
    {
        var token = NewToken();

        token.Revoke(Now.AddHours(1), "Logout");

        Assert.True(token.IsRevoked);
        Assert.False(token.IsValid(Now.AddHours(2)));
        Assert.Equal("Logout", token.RevokedReason);
    }

    [Fact]
    public void Revogar_Token_Ja_Revogado_Lanca_Excecao()
    {
        var token = NewToken();
        token.Revoke(Now);

        Assert.Throws<DomainRuleException>(() => token.Revoke(Now.AddHours(1)));
    }

    [Fact]
    public void Revogar_Se_Ativo_Nao_Lanca_Em_Token_Ja_Revogado()
    {
        var token = NewToken();
        token.Revoke(Now, "primeira");

        // Rotação: revogar novamente não deve falhar.
        token.RevokeIfActive(Now.AddHours(1), "segunda");

        Assert.Equal("primeira", token.RevokedReason);
    }

    [Fact]
    public void Revogar_Se_Ativo_Revoga_Token_Ativo()
    {
        var token = NewToken();

        token.RevokeIfActive(Now.AddHours(1), "Rotação");

        Assert.True(token.IsRevoked);
        Assert.Equal("Rotação", token.RevokedReason);
    }

    // ---------- Isolamento ----------

    [Fact]
    public void Token_Pertence_Ao_Tenant_Do_Usuario()
    {
        var token = NewToken();

        Assert.Equal(TenantId, token.TenantId);
    }
}
