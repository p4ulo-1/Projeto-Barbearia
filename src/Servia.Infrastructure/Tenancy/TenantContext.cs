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
    // Instancia, nao estatico: cada TenantContext tem seu proprio slot.
    // Um AsyncLocal estatico faria dois contextos distintos compartilharem
    // o mesmo valor, e o tenant de um vazaria para o outro.
    private readonly AsyncLocal<Guid?> _current = new();

    public Guid? TenantId => _current.Value;

    public bool HasTenant => _current.Value.HasValue;

    public void SetTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("O tenant não pode ser vazio.", nameof(tenantId));
        }

        _current.Value = tenantId;
    }

    public void Clear() => _current.Value = null;
}
