using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Application.Billing;

/// <summary>
/// Verificação do limite de vagas do plano, executada atomicamente.
///
/// <b>Por que existe.</b> Toda transição que torna um profissional elegível
/// consome uma vaga: criar com serviço, associar o primeiro serviço ativo,
/// reativar. Se a contagem e a gravação não forem atômicas, duas requisições
/// concorrentes contam a mesma vaga livre e ambas gravam — o limite é
/// ultrapassado sem que nenhuma regra tenha sido violada isoladamente.
///
/// <b>Como protege.</b> A operação inteira roda em transação serializável. O
/// PostgreSQL aborta uma das transações concorrentes com erro de
/// serialização; o chamador traduz em conflito de negócio. A proteção não
/// depende de estado em memória nem de um lock aplicativo.
///
/// <b>Reutilizável.</b> A regra de "quantas vagas a operação consome" é um
/// parâmetro. Team usa 1 ao tornar alguém elegível; Billing pode usar o
/// mesmo serviço ao aplicar mudança de plano.
/// </summary>
public sealed class PlanSlotService(
    ISubscriptionRepository subscriptions,
    IPlanRepository plans,
    IProfessionalCountReader professionalCounts)
{
    /// <summary>
    /// Executa <paramref name="operation"/> dentro de uma transação
    /// serializável, garantindo que o limite do plano seja respeitado.
    ///
    /// <summary>
    /// Executa <paramref name="operation"/> dentro de uma transação
    /// serializável, garantindo que o limite do plano seja respeitado.
    ///
    /// <paramref name="slotsConsumed"/> decide, <b>dentro da transação</b>,
    /// quantas vagas a operação consome. A decisão precisa ser feita ali:
    /// depende do estado lido na mesma transação (o vínculo já existe? o
    /// profissional já ocupava vaga?). Decidir fora permitiria contar uma
    /// vaga que a operação não consome — bloqueando uma associação idempotente
    /// ou um segundo serviço de quem já ocupava vaga.
    ///
    /// Zero ou negativo significa que a operação não consome vaga: o limite
    /// não é verificado.
    ///
    /// Lança <see cref="DomainRuleException"/> quando o limite seria
    /// ultrapassado. A exceção aborta a transação: nada é gravado.
    /// </summary>
    public Task<T> ExecuteWithSlotCheckAsync<T>(
        Guid tenantId,
        Func<CancellationToken, Task<int>> slotsConsumed,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(slotsConsumed);
        ArgumentNullException.ThrowIfNull(operation);

        return subscriptions.ExecuteSerializableAsync(
            async token =>
            {
                var additionalSlots = await slotsConsumed(token);

                if (additionalSlots > 0)
                {
                    await EnsureSlotsAvailableAsync(tenantId, additionalSlots, token);
                }

                return await operation(token);
            },
            cancellationToken);
    }

    /// <summary>
    /// Verifica se o plano comporta <paramref name="additionalSlots"/> vagas
    /// além das já ocupadas.
    ///
    /// Sem assinatura ou com plano ilimitado, não há limite a aplicar.
    /// </summary>
    public async Task EnsureSlotsAvailableAsync(
        Guid tenantId,
        int additionalSlots,
        CancellationToken cancellationToken = default)
    {
        if (additionalSlots <= 0)
        {
            return;
        }

        var subscription = await subscriptions.FindCurrentAsync(tenantId, cancellationToken);

        if (subscription is null)
        {
            // Sem assinatura, não há limite de plano a aplicar.
            return;
        }

        var plan = await plans.FindByIdAsync(subscription.PlanId, cancellationToken);

        if (plan is null || plan.HasUnlimitedProfessionals)
        {
            return;
        }

        var current = await professionalCounts.CountEligibleAsync(tenantId, cancellationToken);

        if (current + additionalSlots > plan.IncludedProfessionals)
        {
            throw new DomainRuleException(
                $"O plano {plan.Name} comporta até {plan.IncludedProfessionals} profissionais "
                + $"e o estabelecimento já tem {current}. "
                + "Faça upgrade do plano para adicionar mais prestadores de serviço.");
        }
    }
}