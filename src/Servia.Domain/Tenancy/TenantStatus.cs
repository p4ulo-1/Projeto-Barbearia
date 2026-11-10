namespace Servia.Domain.Tenancy;

/// <summary>
/// Ciclo de vida do estabelecimento na plataforma.
///
/// <see cref="Trial"/> e <see cref="Active"/> permitem operação normal.
/// <see cref="Suspended"/> bloqueia escrita, mas mantém leitura e agenda
/// durante o período de carência (ARCH.md §13.2).
/// <see cref="Cancelled"/> é terminal.
/// </summary>
public enum TenantStatus
{
    /// <summary>Período de teste, com acesso completo.</summary>
    Trial = 1,

    /// <summary>Assinatura ativa e em dia.</summary>
    Active = 2,

    /// <summary>Inadimplente: escrita bloqueada, leitura e agenda mantidas.</summary>
    Suspended = 3,

    /// <summary>Encerrado. Estado terminal.</summary>
    Cancelled = 4
}
