namespace Servia.Domain.Tenancy;

/// <summary>
/// Marca uma entidade de negócio que pertence a um estabelecimento.
///
/// É a base do filtro global do EF Core (ADR-016): toda entidade que
/// implementa esta interface tem suas consultas automaticamente restritas ao
/// tenant corrente. Sem o filtro, um usuário poderia ler dados de outro
/// estabelecimento.
///
/// <b>Exceções intencionais:</b> <see cref="Tenant"/> é a raiz do agregado e
/// não implementa esta interface (seria autorreferência). <see cref="Plan"/>
/// é entidade global da plataforma, compartilhada por todos os tenants.
/// </summary>
public interface ITenantScoped
{
    /// <summary>Identificador do estabelecimento ao qual o dado pertence.</summary>
    Guid TenantId { get; }
}
