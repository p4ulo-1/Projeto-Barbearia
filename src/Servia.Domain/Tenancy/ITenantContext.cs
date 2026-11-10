namespace Servia.Domain.Tenancy;

/// <summary>
/// Expõe o tenant da requisição corrente.
///
/// O valor é resolvido <b>no servidor</b> — por subdomínio ou por claim do
/// JWT — e nunca a partir de um valor enviado livremente pelo cliente
/// (ARCH.md §3.2). É a fonte do filtro global do EF Core.
///
/// Quando não há tenant resolvido, <see cref="TenantId"/> é nulo. Nesse caso
/// o filtro global deve negar acesso a dados de negócio, e não liberar tudo.
/// </summary>
public interface ITenantContext
{
    /// <summary>Tenant resolvido para a requisição, ou nulo se não houver.</summary>
    Guid? TenantId { get; }

    /// <summary>Indica se há tenant resolvido.</summary>
    bool HasTenant { get; }

    /// <summary>
    /// Define o tenant da requisição. Chamado apenas pela camada que resolve
    /// o tenant no servidor.
    /// </summary>
    void SetTenant(Guid tenantId);

    /// <summary>Limpa o tenant corrente.</summary>
    void Clear();
}
