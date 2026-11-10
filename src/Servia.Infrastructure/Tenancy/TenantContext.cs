using Servia.Domain.Tenancy;

namespace Servia.Infrastructure.Tenancy;

/// <summary>
/// Implementação de <see cref="ITenantContext"/> baseada em <see cref="AsyncLocal{T}"/>.
///
/// O valor flui pela cadeia assíncrona da requisição sem vazar entre
/// requisições concorrentes: cada uma tem seu próprio contexto lógico.
///
/// Registrado como <b>singleton</b>: o estado mutável vive no
/// <c>AsyncLocal</c>, não na instância.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    private static readonly AsyncLocal<Guid?> Current = new();

    public Guid? TenantId => Current.Value;

    public bool HasTenant => Current.Value.HasValue;

    public void SetTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("O tenant não pode ser vazio.", nameof(tenantId));
        }

        Current.Value = tenantId;
    }

    public void Clear() => Current.Value = null;
}
