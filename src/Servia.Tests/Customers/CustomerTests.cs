using Servia.Domain.Common;
using Servia.Domain.Customers;

namespace Servia.Tests.Customers;

/// <summary>
/// Invariantes do cliente: contato obrigatório, cadastro por tenant,
/// vínculo opcional com usuário e anonimização para LGPD.
/// </summary>
public class CustomerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private static Customer NewCustomer() =>
        Customer.Create(TenantId, "João das Coves", "21999998888",
            email: "joao@exemplo.com",
            birthDate: new DateOnly(1990, 5, 20),
            now: Now);

    [Fact]
    public void Criar_Cliente_Valido_Inicia_Ativo()
    {
        var customer = NewCustomer();

        Assert.Equal("João das Coves", customer.Name);
        Assert.Equal("21999998888", customer.Phone);
        Assert.Equal("joao@exemplo.com", customer.Email);
        Assert.True(customer.IsActive);
        Assert.False(customer.IsAnonymized);
        Assert.Equal(Now, customer.CreatedAt);
    }

    [Fact]
    public void Criar_Cliente_Sem_Tenant_Lanca_Excecao()
    {
        Assert.Throws<DomainRuleException>(() =>
            Customer.Create(Guid.Empty, "João", "21999998888"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_Cliente_Sem_Nome_Lanca_Excecao(string name)
    {
        Assert.Throws<DomainRuleException>(() =>
            Customer.Create(TenantId, name, "21999998888"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_Cliente_Sem_Telefone_Lanca_Excecao(string phone)
    {
        Assert.Throws<DomainRuleException>(() =>
            Customer.Create(TenantId, "João", phone));
    }

    [Fact]
    public void Cliente_Pode_Existir_Sem_Usuario_De_Login()
    {
        var customer = NewCustomer();

        Assert.Null(customer.UserId);
    }

    [Fact]
    public void Vincular_Usuario_Registra_Identificador()
    {
        var customer = NewCustomer();
        var userId = Guid.NewGuid();

        customer.LinkUser(userId);

        Assert.Equal(userId, customer.UserId);
    }

    [Fact]
    public void Atualizar_Contato_Altera_Dados()
    {
        var customer = NewCustomer();

        customer.UpdateContact("João Silva", "21888887777", "joao.silva@exemplo.com", null);

        Assert.Equal("João Silva", customer.Name);
        Assert.Equal("21888887777", customer.Phone);
        Assert.Equal("joao.silva@exemplo.com", customer.Email);
        Assert.Null(customer.BirthDate);
    }

    [Fact]
    public void Atualizar_Contato_Sem_Telefone_Lanca_Excecao()
    {
        var customer = NewCustomer();

        Assert.Throws<DomainRuleException>(() =>
            customer.UpdateContact("João", "", null, null));
    }

    // ---------- LGPD: anonimização ----------

    [Fact]
    public void Anonimizar_Remove_Dados_Pessoais()
    {
        var customer = NewCustomer();
        customer.LinkUser(Guid.NewGuid());

        customer.Anonymize();

        Assert.True(customer.IsAnonymized);
        Assert.Equal("Cliente anonimizado", customer.Name);
        Assert.Equal("0000000000", customer.Phone);
        Assert.Null(customer.Email);
        Assert.Null(customer.BirthDate);
        Assert.Null(customer.UserId);
        Assert.False(customer.IsActive);
    }

    [Fact]
    public void Anonimizar_Preserva_Identidade_Do_Registro()
    {
        var customer = NewCustomer();
        var originalId = customer.Id;

        customer.Anonymize();

        // O registro permanece para o histórico de atendimentos.
        Assert.Equal(originalId, customer.Id);
    }

    [Fact]
    public void Nao_E_Possivel_Alterar_Cadastro_Anonimizado()
    {
        var customer = NewCustomer();
        customer.Anonymize();

        Assert.Throws<DomainRuleException>(() =>
            customer.UpdateContact("Outro nome", "2100000000", null, null));
    }

    [Fact]
    public void Desativar_E_Reativar_Alteram_Estado()
    {
        var customer = NewCustomer();

        customer.Deactivate();
        Assert.False(customer.IsActive);

        customer.Activate();
        Assert.True(customer.IsActive);
    }
}
