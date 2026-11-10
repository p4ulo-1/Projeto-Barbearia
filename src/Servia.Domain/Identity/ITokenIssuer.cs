namespace Servia.Domain.Identity;

/// <summary>
/// Contrato de emissão de token de acesso (F10.1).
///
/// O token carrega a identidade e o tenant: <c>UserId</c>, <c>TenantId</c> e
/// <c>Role</c>. É a fonte confiável para o servidor resolver o tenant — o
/// cliente não informa o tenant, ele vem assinado no token (F10.5).
/// </summary>
public interface ITokenIssuer
{
    /// <summary>
    /// Emite um token de acesso para o usuário.
    /// </summary>
    AccessToken Issue(User user, DateTimeOffset now);
}

/// <summary>Token de acesso emitido.</summary>
public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);
