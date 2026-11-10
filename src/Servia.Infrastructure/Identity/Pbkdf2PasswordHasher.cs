using System.Security.Cryptography;
using Servia.Domain.Identity;

namespace Servia.Infrastructure.Identity;

/// <summary>
/// Hash de senha com PBKDF2-HMACSHA256 (F10.2).
///
/// Escolhido por ser parte da biblioteca padrão do .NET: sem dependência
/// externa, sem superfície de ataque adicional.
///
/// Formato armazenado: <c>iterações.salt.hash</c>, em Base64. O número de
/// iterações fica gravado junto, permitindo aumentar o custo no futuro sem
/// invalidar hashes antigos.
///
/// A verificação usa <see cref="CryptographicOperations.FixedTimeEquals"/>,
/// que compara em tempo constante e não vaza informação por timing.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    /// <summary>Iterações atuais. OWASP recomenda ao menos 600.000 para PBKDF2-SHA256.</summary>
    public const int DefaultIterations = 600_000;

    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;
    private const char Separator = '.';

    private readonly int _iterations;

    public Pbkdf2PasswordHasher(int iterations = DefaultIterations)
    {
        if (iterations <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(iterations), "O número de iterações deve ser maior que zero.");
        }

        _iterations = iterations;
    }

    public string Hash(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("A senha é obrigatória.", nameof(password));
        }

        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hash = Derive(password, salt, _iterations, HashSizeBytes);

        return string.Join(
            Separator,
            _iterations.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash))
        {
            return false;
        }

        var parts = hash.Split(Separator);

        if (parts.Length != 3)
        {
            return false;
        }

        if (!int.TryParse(
                parts[0],
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var iterations)
            || iterations <= 0)
        {
            return false;
        }

        byte[] salt;
        byte[] expected;

        try
        {
            salt = Convert.FromBase64String(parts[1]);
            expected = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (salt.Length == 0 || expected.Length == 0)
        {
            return false;
        }

        var actual = Derive(password, salt, iterations, expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static byte[] Derive(string password, byte[] salt, int iterations, int length) =>
        Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            length);
}
