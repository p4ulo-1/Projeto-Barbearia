using Servia.Domain.Common;
using Servia.Domain.Memberships;

namespace Servia.Tests.Memberships;

/// <summary>
/// F7.2 — pacotes de serviços com saldo e validade.
/// F7.3 — controle de benefícios (consumo do pacote).
/// </summary>
public class ServicePackageTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly DateOnly PurchaseDate = new(2026, 11, 10);

    private static ServicePackage NewPackage() =>
        ServicePackage.Create(TenantId, "Pacote 10 cortes", Money.From(400m), 10, 180);

    private static PackageUsage NewUsage() =>
        PackageUsage.Purchase(TenantId, CustomerId, NewPackage(), PurchaseDate);

    // ---------- F7.2: pacote ----------

    [Fact]
    public void Criar_Pacote_Valido()
    {
        var package = NewPackage();

        Assert.Equal("Pacote 10 cortes", package.Name);
        Assert.Equal(400m, package.Price.Amount);
        Assert.Equal(10, package.SessionsIncluded);
        Assert.Equal(180, package.ValidityDays);
        Assert.True(package.IsActive);
    }

    [Fact]
    public void Criar_Pacote_Sem_Sessoes_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            ServicePackage.Create(TenantId, "Pacote", Money.From(400m), 0, 180));
    }

    [Fact]
    public void Criar_Pacote_Com_Validade_Zero_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            ServicePackage.Create(TenantId, "Pacote", Money.From(400m), 10, 0));
    }

    [Fact]
    public void Criar_Pacote_Com_Preco_Zero_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            ServicePackage.Create(TenantId, "Pacote", Money.From(0m), 10, 180));
    }

    // ---------- Compra ----------

    [Fact]
    public void Comprar_Pacote_Define_Saldo_E_Validade()
    {
        var usage = NewUsage();

        Assert.Equal(10, usage.SessionsRemaining);
        Assert.Equal(PurchaseDate.AddDays(180), usage.ExpiresAt);
        Assert.False(usage.IsExhausted);
    }

    [Fact]
    public void Comprar_Pacote_Inativo_Lanca_Excecao()
    {
        var package = NewPackage();
        package.Deactivate();

        Assert.Throws<DomainRuleException>(() =>
            PackageUsage.Purchase(TenantId, CustomerId, package, PurchaseDate));
    }

    [Fact]
    public void Comprar_Sem_Cliente_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            PackageUsage.Purchase(TenantId, Guid.Empty, NewPackage(), PurchaseDate));
    }

    // ---------- F7.3: consumo ----------

    [Fact]
    public void Consumir_Sessao_Reduz_Saldo()
    {
        var usage = NewUsage();

        usage.Consume(PurchaseDate);

        Assert.Equal(9, usage.SessionsRemaining);
    }

    [Fact]
    public void Consumir_Todas_As_Sessoes_Esgota_O_Pacote()
    {
        var usage = NewUsage();

        for (var index = 0; index < 10; index++)
        {
            usage.Consume(PurchaseDate);
        }

        Assert.True(usage.IsExhausted);
        Assert.Throws<DomainRuleException>(() => usage.Consume(PurchaseDate));
    }

    [Fact]
    public void Consumir_Apos_Expirar_Lanca_Excecao()
    {
        var usage = NewUsage();
        var afterExpiry = PurchaseDate.AddDays(181);

        Assert.True(usage.IsExpired(afterExpiry));
        Assert.Throws<DomainRuleException>(() => usage.Consume(afterExpiry));
    }

    [Fact]
    public void Consumir_No_Ultimo_Dia_De_Validade_Eh_Permitido()
    {
        var usage = NewUsage();
        var lastDay = PurchaseDate.AddDays(180);

        Assert.False(usage.IsExpired(lastDay));
        usage.Consume(lastDay);

        Assert.Equal(9, usage.SessionsRemaining);
    }
}
