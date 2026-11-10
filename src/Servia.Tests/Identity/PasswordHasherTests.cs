using Servia.Infrastructure.Identity;

namespace Servia.Tests.Identity;

/// <summary>
/// F10.2 — hash de senha.
///
/// Verifica que a senha nunca é armazenada em claro, que o salt é único por
/// senha e que a verificação rejeita hash malformado sem lançar.
/// </summary>
public class PasswordHasherTests
{
    // Iterações baixas para o teste ser rápido; o padrão é 600.000.
    private readonly Pbkdf2PasswordHasher _hasher = new(iterations: 1_000);

    [Fact]
    public void Hash_Nao_Contem_A_Senha_Em_Claro()
    {
        const string password = "MinhaSenhaSegura123";

        var hash = _hasher.Hash(password);

        Assert.DoesNotContain(password, hash, StringComparison.Ordinal);
    }

    [Fact]
    public void Hash_De_Senhas_Iguais_Eh_Diferente()
    {
        const string password = "MesmaSenha123";

        var first = _hasher.Hash(password);
        var second = _hasher.Hash(password);

        // Salt aleatório por senha: hashes diferentes para a mesma entrada.
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Verificar_Senha_Correta_Retorna_Verdadeiro()
    {
        const string password = "SenhaCorreta123";
        var hash = _hasher.Hash(password);

        Assert.True(_hasher.Verify(password, hash));
    }

    [Fact]
    public void Verificar_Senha_Errada_Retorna_Falso()
    {
        var hash = _hasher.Hash("SenhaCorreta123");

        Assert.False(_hasher.Verify("SenhaErrada123", hash));
    }

    [Fact]
    public void Verificar_Senha_Com_Caixa_Diferente_Retorna_Falso()
    {
        var hash = _hasher.Hash("SenhaCorreta123");

        Assert.False(_hasher.Verify("senhacorreta123", hash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("sem-separador")]
    [InlineData("abc.def")]
    [InlineData("nao-numero.c2FsdA==.aGFzaA==")]
    [InlineData("1000.@@@.@@@")]
    public void Verificar_Hash_Malformado_Retorna_Falso_Sem_Lancar(string hash)
    {
        Assert.False(_hasher.Verify("qualquer", hash));
    }

    [Fact]
    public void Verificar_Senha_Vazia_Retorna_Falso()
    {
        var hash = _hasher.Hash("Senha123");

        Assert.False(_hasher.Verify(string.Empty, hash));
    }

    [Fact]
    public void Hash_Registra_As_Iteracoes_Usadas()
    {
        var hash = _hasher.Hash("Senha123");

        Assert.StartsWith("1000.", hash, StringComparison.Ordinal);
    }

    [Fact]
    public void Hash_Com_Iteracoes_Invalidas_Lanca_Excecao()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Pbkdf2PasswordHasher(0));
    }

    [Fact]
    public void Hash_De_Senha_Vazia_Lanca_Excecao()
    {
        Assert.Throws<ArgumentException>(() => _hasher.Hash(string.Empty));
    }
}
