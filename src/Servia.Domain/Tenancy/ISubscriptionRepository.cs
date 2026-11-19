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
    /// Retorna a assinatura vigente ou, na sua ausência, a última assinatura que concedeu acesso.
    /// Tentativas sem pagamento confirmado ficam fora da consulta de plano efetivo.
    /// </summary>
    Task<Subscription?> FindCurrentAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<Subscription?> FindCheckoutAsync(Guid tenantId, Guid checkoutKey,
        CancellationToken cancellationToken = default, bool readOnly = false);

    Task<bool> HasPendingCheckoutAsync(Guid tenantId, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Executa a operação dentro de uma transação com nível de isolamento
    /// serializável.
    ///
    /// Usado pelas transições que consomem ou liberam vaga no plano: a
    /// contagem e a gravação precisam ser atômicas, senão duas requisições
    /// concorrentes contam a mesma vaga livre e ambas gravam.
    ///
    /// O nível serializável faz o PostgreSQL abortar uma das transações
    /// concorrentes. A infraestrutura traduz o SQLSTATE em
    /// ConcurrencyConflictException e limpa o rastreamento após rollback.
    /// O delegate não é repetido automaticamente: uma tentativa posterior
    /// deve usar uma nova unidade de trabalho.
    /// </summary>
    Task<T> ExecuteSerializableAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
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
/// Leitura da quantidade de profissionais que ocupam vaga no plano (F16.4).
///
/// Separado do repositório de assinatura porque a regra de faixa depende
/// da equipe, não da assinatura.
///
/// <b>Conta apenas quem ocupa vaga</b>, conforme o critério aprovado
/// (ESCOPO_AV2 §2.3): profissional ativo, com ao menos um serviço ativo
/// associado e com jornada configurada. Não conta cargo, usuário vinculado
/// nem cadastro isolado, e não varia por disponibilidade momentânea.
/// </summary>
public interface IProfessionalCountReader
{
    /// <summary>
    /// Conta os profissionais do tenant que ocupam vaga no plano.
    /// </summary>
    Task<int> CountEligibleAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
