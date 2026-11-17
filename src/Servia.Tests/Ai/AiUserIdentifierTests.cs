using Servia.Domain.Ai;

namespace Servia.Tests.Ai;

/// <summary>
/// Identificador técnico opaco enviado ao provedor de IA
/// (gestão de IA, §4.2; plano de implementação, Etapa 2).
///
/// O valor é derivado por HMAC-SHA-256 com segredo exclusivo do backend. É
/// estável para a mesma chave e versão, não contém dados pessoais e não pode
/// ser reproduzido sem o segredo.
/// </summary>
public class AiUserIdentifierTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private const string Secret = "segredo-exclusivo-do-backend";
    private const string OtherSecret = "outro-segredo";
    private const string Version = "v1";

    [Fact]
    public void Identificador_Do_Tenant_Eh_Estavel()
    {
        var first = AiUserIdentifier.ForTenant(TenantId, Secret, Version);
        var second = AiUserIdentifier.ForTenant(TenantId, Secret, Version);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Tenants_Diferentes_Geram_Identificadores_Diferentes()
    {
        Assert.NotEqual(
            AiUserIdentifier.ForTenant(TenantId, Secret, Version),
            AiUserIdentifier.ForTenant(OtherTenantId, Secret, Version));
    }

    [Fact]
    public void Segredos_Diferentes_Geram_Identificadores_Diferentes()
    {
        Assert.NotEqual(
            AiUserIdentifier.ForTenant(TenantId, Secret, Version),
            AiUserIdentifier.ForTenant(TenantId, OtherSecret, Version));
    }

    [Fact]
    public void Versoes_Diferentes_Geram_Identificadores_Diferentes()
    {
        Assert.NotEqual(
            AiUserIdentifier.ForTenant(TenantId, Secret, "v1"),
            AiUserIdentifier.ForTenant(TenantId, Secret, "v2"));
    }

    [Fact]
    public void Identificador_Inclui_A_Versao_Da_Chave()
    {
        var value = AiUserIdentifier.ForTenant(TenantId, Secret, "v7");

        Assert.StartsWith("tenant_v7_", value, StringComparison.Ordinal);
    }

    [Fact]
    public void Identificador_Nao_Contem_O_Guid_Original()
    {
        var identifier = AiUserIdentifier.ForTenant(TenantId, Secret, Version);

        Assert.DoesNotContain(TenantId.ToString(), identifier, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(TenantId.ToString("N"), identifier, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Identificador_Nao_Contem_O_Segredo()
    {
        var identifier = AiUserIdentifier.ForTenant(TenantId, Secret, Version);

        Assert.DoesNotContain(Secret, identifier, StringComparison.Ordinal);
    }

    [Fact]
    public void Identificador_Do_Usuario_Distingue_Do_Identificador_Do_Tenant()
    {
        Assert.NotEqual(
            AiUserIdentifier.ForTenant(TenantId, Secret, Version),
            AiUserIdentifier.ForUser(TenantId, UserId, Secret, Version));
    }

    [Fact]
    public void Usuario_Eh_Escopado_Pelo_Tenant()
    {
        Assert.NotEqual(
            AiUserIdentifier.ForUser(TenantId, UserId, Secret, Version),
            AiUserIdentifier.ForUser(OtherTenantId, UserId, Secret, Version));
    }

    [Fact]
    public void Rejeita_Tenant_Vazio()
    {
        Assert.Throws<ArgumentException>(
            () => AiUserIdentifier.ForTenant(Guid.Empty, Secret, Version));
    }

    [Fact]
    public void Rejeita_Usuario_Vazio()
    {
        Assert.Throws<ArgumentException>(
            () => AiUserIdentifier.ForUser(TenantId, Guid.Empty, Secret, Version));
    }

    [Fact]
    public void Rejeita_Segredo_Vazio()
    {
        Assert.Throws<ArgumentException>(
            () => AiUserIdentifier.ForTenant(TenantId, string.Empty, Version));
    }

    [Fact]
    public void Rejeita_Versao_Vazia()
    {
        Assert.Throws<ArgumentException>(
            () => AiUserIdentifier.ForTenant(TenantId, Secret, string.Empty));
    }
}
