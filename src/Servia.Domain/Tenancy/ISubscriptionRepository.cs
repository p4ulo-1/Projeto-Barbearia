namespace Servia.Domain.Tenancy;

/// <summary>
/// Contrato de persistência de assinaturas (F16).
///
/// Todas as consultas são escopadas por tenant: o repositório nunca expõe
/// um método que atravesse a fronteira de isolamento.
/// </summary>
public interface ISubscriptionRepository
{
    /// <summary>
    /// Retorna a assinatura vigente do tenant.
    /// Retorna nulo quando o tenant ainda não contratou.
    /// </summary>
    Task<Subscription?> FindCurrentAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Registra uma nova assinatura.</summary>
    Task AddAsync(Subscription subscription, CancellationToken cancellationToken = default);

    /// <summary>Registra uma mudança de plano no histórico.</summary>
    Task AddChangeAsync(PlanChange change, CancellationToken cancellationToken = default);

    /// <summary>Lista o histórico de mudanças de plano do tenant.</summary>
    Task<IReadOnlyList<PlanChange>> ListChangesAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>Persiste as alterações pendentes.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Contrato de consulta de planos (F16).
///
/// <c>Plan</c> é entidade global: não pertence a um tenant.
/// </summary>
public interface IPlanRepository
{
    /// <summary>Busca o plano pelo código comercial.</summary>
    Task<Plan?> FindByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>Busca o plano pelo identificador.</summary>
    Task<Plan?> FindByIdAsync(Guid planId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna o menor plano ativo que comporta a quantidade de
    /// profissionais informada (F16.4).
    /// </summary>
    Task<Plan?> FindSmallestForAsync(
        int professionalCount,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Leitura da quantidade de profissionais ativos do tenant (F16.4).
///
/// Separado do repositório de assinatura porque a regra de faixa depende
/// da equipe, não da assinatura.
/// </summary>
public interface IProfessionalCountReader
{
    /// <summary>Conta os profissionais ativos do tenant.</summary>
    Task<int> CountActiveAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
