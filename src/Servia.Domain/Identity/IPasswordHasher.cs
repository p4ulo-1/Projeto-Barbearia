namespace Servia.Domain.Identity;

/// <summary>
/// Contrato de hash de senha (F10.2).
///
/// A senha nunca é armazenada em claro. O hash é irreversível e inclui salt
/// aleatório por senha, de modo que duas senhas iguais produzam hashes
/// diferentes.
///
/// A verificação é resistente a timing: compara em tempo constante, para que
/// um atacante não descubra a senha medindo o tempo de resposta.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Gera o hash de uma senha em claro.</summary>
    string Hash(string password);

    /// <summary>
    /// Verifica se a senha em claro corresponde ao hash armazenado.
    /// Retorna falso quando o hash é inválido ou malformado — nunca lança.
    /// </summary>
    bool Verify(string password, string hash);
}
