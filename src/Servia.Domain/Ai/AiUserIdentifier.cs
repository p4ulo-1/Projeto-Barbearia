using System.Security.Cryptography;
using System.Text;

namespace Servia.Domain.Ai;

/// <summary>
/// Identificador técnico opaco enviado ao provedor de IA
/// (gestão de IA, §4.2; plano de implementação, Etapa 2).
///
/// <b>Por que HMAC e não hash simples:</b> o identificador de origem é um
/// `Guid` de tenant, previsível e enumerável. Um SHA-256 sem chave permitiria
/// confirmar a existência de um tenant específico por tentativa. O HMAC usa um
/// segredo exclusivo do backend, de modo que o valor só pode ser reproduzido
/// por quem possui a chave.
///
/// <b>O que ele não é:</b> não é o `tenant_id`, não é autorização e não
/// substitui o controle de acesso do Servia. O tenant continua vindo
/// exclusivamente do contexto autenticado.
///
/// <b>Sem dados pessoais:</b> o valor é derivado do identificador interno, de
/// modo que e-mail, telefone, nome ou qualquer dado pessoal nunca chega ao
/// provedor.
///
/// <b>Versão da chave:</b> cada identificador registra a versão da chave que o
/// gerou. Na rotação normal, novos identificadores usam a nova versão e os já
/// persistidos permanecem estáveis — a chave anterior deixa de gerar novos
/// valores, mas continua capaz de validar os existentes.
/// </summary>
public static class AiUserIdentifier
{
    /// <summary>Prefixo que distingue o identificador de tenant.</summary>
    private const string TenantPrefix = "tenant";

    /// <summary>Prefixo que distingue o identificador de usuário.</summary>
    private const string UserPrefix = "user";

    /// <summary>
    /// Gera o identificador opaco do tenant com a chave informada.
    /// </summary>
    /// <param name="tenantId">Identificador interno do estabelecimento.</param>
    /// <param name="secret">Segredo exclusivo do backend, fora do Git.</param>
    /// <param name="keyVersion">Versão da chave usada na geração.</param>
    public static string ForTenant(Guid tenantId, string secret, string keyVersion)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("O estabelecimento é obrigatório.", nameof(tenantId));
        }

        return Build(TenantPrefix, secret, keyVersion, [tenantId]);
    }

    /// <summary>
    /// Gera um identificador regenerado para rotação emergencial. O nonce
    /// garante um valor novo mesmo com a mesma chave e versão.
    /// </summary>
    public static string ForTenantRotated(
        Guid tenantId,
        string secret,
        string keyVersion,
        string nonce)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("O estabelecimento é obrigatório.", nameof(tenantId));
        }

        if (string.IsNullOrWhiteSpace(nonce))
        {
            throw new ArgumentException("O nonce da rotação é obrigatório.", nameof(nonce));
        }

        return Build(TenantPrefix, secret, keyVersion, [tenantId], nonce);
    }

    /// <summary>
    /// Gera o identificador opaco de um usuário dentro do tenant.
    /// </summary>
    public static string ForUser(Guid tenantId, Guid userId, string secret, string keyVersion)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("O estabelecimento é obrigatório.", nameof(tenantId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("O usuário é obrigatório.", nameof(userId));
        }

        return Build(UserPrefix, secret, keyVersion, [tenantId, userId]);
    }

    /// <summary>
    /// Deriva o identificador por HMAC-SHA-256. O resultado é estável para a
    /// mesma chave e versão, opaco e não contém o valor original.
    ///
    /// O valor inclui a versão da chave para que a rotação seja rastreável sem
    /// alterar os identificadores já emitidos.
    ///
    /// <paramref name="nonce"/> permite a rotação emergencial: sem ele, a mesma
    /// chave e o mesmo tenant produziriam sempre o mesmo valor, e a regeneração
    /// não teria efeito. O nonce não é segredo — apenas garante um valor novo.
    /// </summary>
    private static string Build(
        string kind,
        string secret,
        string keyVersion,
        Guid[] parts,
        string? nonce = null)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new ArgumentException("O segredo do identificador é obrigatório.", nameof(secret));
        }

        if (string.IsNullOrWhiteSpace(keyVersion))
        {
            throw new ArgumentException("A versão da chave é obrigatória.", nameof(keyVersion));
        }

        var raw = new StringBuilder(kind);

        foreach (var part in parts)
        {
            raw.Append(':').Append(part.ToString("N"));
        }

        if (!string.IsNullOrWhiteSpace(nonce))
        {
            raw.Append(":nonce:").Append(nonce);
        }

        var mac = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes(raw.ToString()));

        // 16 bytes bastam para unicidade prática e mantêm o valor curto.
        return $"{kind}_{keyVersion}_{Convert.ToHexString(mac, 0, 16).ToLowerInvariant()}";
    }
}
