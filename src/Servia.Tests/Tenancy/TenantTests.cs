using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Tests.Tenancy;

/// <summary>
/// Invariantes do agregado Tenant: slug válido e imutável, ciclo de vida
/// com estados terminais e permissões de leitura e escrita.
/// </summary>
public class TenantTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private static Tenant NewTenant(bool startOnTrial = true) =>
        Tenant.Create("barbearia-xyz", "Barbearia XYZ", SegmentType.Barbershop,
            "12.345.678/0001-90", Now, startOnTrial);

    // ---------- Criação ----------

    [Fact]
    public void Criar_Tenant_Valido_Inicia_Em_Trial()
    {
        var tenant = NewTenant();

        Assert.Equal("barbearia-xyz", tenant.Slug);
        Assert.Equal("Barbearia XYZ", tenant.Name);
        Assert.Equal(SegmentType.Barbershop, tenant.Segment);
        Assert.Equal(TenantStatus.Trial, tenant.Status);
        Assert.True(tenant.IsOnTrial);
        Assert.Equal(Now, tenant.CreatedAt);
    }

    [Fact]
    public void Criar_Tenant_Sem_Trial_Inicia_Ativo()
    {
        var tenant = NewTenant(startOnTrial: false);

        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.False(tenant.IsOnTrial);
    }

    [Fact]
    public void Slug_Eh_Normalizado_Para_Minusculas()
    {
        var tenant = Tenant.Create("Barbearia-XYZ", "Barbearia", SegmentType.Barbershop,
            "123", Now);

        Assert.Equal("barbearia-xyz", tenant.Slug);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_Tenant_Sem_Slug_Lanca_Excecao(string slug)
    {
        Assert.Throws<DomainRuleException>(() =>
            Tenant.Create(slug, "Barbearia", SegmentType.Barbershop, "123", Now));
    }

    [Theory]
    [InlineData("ab")]                 // curto demais
    [InlineData("barbearia_xyz")]      // underscore nao permitido
    [InlineData("barbearia xyz")]      // espaco nao permitido
    [InlineData("-barbearia")]         // comeca com hifen
    [InlineData("barbearia-")]         // termina com hifen
    public void Criar_Tenant_Com_Slug_Invalido_Lanca_Excecao(string slug)
    {
        Assert.Throws<DomainRuleException>(() =>
            Tenant.Create(slug, "Barbearia", SegmentType.Barbershop, "123", Now));
    }

    [Fact]
    public void Criar_Tenant_Sem_Nome_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            Tenant.Create("barbearia-xyz", "  ", SegmentType.Barbershop, "123", Now));
    }

    [Fact]
    public void Criar_Tenant_Sem_Documento_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            Tenant.Create("barbearia-xyz", "Barbearia", SegmentType.Barbershop, "", Now));
    }

    // ---------- Slug imutavel ----------

    [Fact]
    public void Atualizar_Perfil_Nao_Altera_Slug()
    {
        var tenant = NewTenant();
        var originalSlug = tenant.Slug;

        tenant.UpdateProfile("Novo Nome", SegmentType.HairSalon, "987");

        Assert.Equal(originalSlug, tenant.Slug);
        Assert.Equal("Novo Nome", tenant.Name);
        Assert.Equal(SegmentType.HairSalon, tenant.Segment);
    }

    // ---------- Ciclo de vida ----------

    [Fact]
    public void Ativar_Converte_Trial_Em_Ativo()
    {
        var tenant = NewTenant();

        tenant.Activate();

        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.True(tenant.CanWrite);
    }

    [Fact]
    public void Suspender_Bloqueia_Escrita_Mas_Mantem_Leitura()
    {
        var tenant = NewTenant(startOnTrial: false);

        tenant.Suspend();

        Assert.Equal(TenantStatus.Suspended, tenant.Status);
        Assert.False(tenant.CanWrite);
        Assert.True(tenant.CanRead);
    }

    [Fact]
    public void Cancelar_Torna_Tenant_Terminal()
    {
        var tenant = NewTenant(startOnTrial: false);

        tenant.Cancel();

        Assert.Equal(TenantStatus.Cancelled, tenant.Status);
        Assert.False(tenant.CanWrite);
        Assert.False(tenant.CanRead);
    }

    [Fact]
    public void Tenant_Cancelado_Nao_Pode_Ser_Ativado()
    {
        var tenant = NewTenant(startOnTrial: false);
        tenant.Cancel();

        Assert.Throws<DomainRuleException>(() => tenant.Activate());
        Assert.Equal(TenantStatus.Cancelled, tenant.Status);
    }

    [Fact]
    public void Tenant_Cancelado_Nao_Pode_Ser_Suspenso()
    {
        var tenant = NewTenant(startOnTrial: false);
        tenant.Cancel();

        Assert.Throws<DomainRuleException>(() => tenant.Suspend());
    }

    [Fact]
    public void Cancelar_Duas_Vezes_Lanca_Excecao()
    {
        var tenant = NewTenant(startOnTrial: false);
        tenant.Cancel();

        Assert.Throws<DomainRuleException>(() => tenant.Cancel());
    }

    [Fact]
    public void Tenant_Suspenso_Pode_Ser_Reativado()
    {
        var tenant = NewTenant(startOnTrial: false);
        tenant.Suspend();

        tenant.Activate();

        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.True(tenant.CanWrite);
    }
}
