using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Loyalty;

/// <summary>
/// Conta de fidelidade de um cliente (F7.5).
///
/// Acumula pontos por atendimento concluído e permite resgate. Os pontos
/// pertencem ao cliente <b>dentro do estabelecimento</b>: a mesma pessoa em
/// dois tenants tem duas contas independentes (ADR-015).
///
/// O saldo nunca fica negativo: resgatar mais do que existe é rejeitado.
/// </summary>
public sealed class LoyaltyAccount : ITenantScoped
{
    private LoyaltyAccount(Guid id, Guid tenantId, Guid customerId)
    {
        Id = id;
        TenantId = tenantId;
        CustomerId = customerId;
        Points = 0;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Open"/>.
    /// </summary>
    private LoyaltyAccount()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid CustomerId { get; private set; }

    /// <summary>Saldo de pontos.</summary>
    public int Points { get; private set; }

    /// <summary>Total de pontos já creditados, para relatórios.</summary>
    public int TotalEarned { get; private set; }

    /// <summary>Total de pontos já resgatados, para relatórios.</summary>
    public int TotalRedeemed { get; private set; }

    public static LoyaltyAccount Open(Guid tenantId, Guid customerId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (customerId == Guid.Empty)
        {
            throw new DomainRuleException("O cliente é obrigatório.");
        }

        return new LoyaltyAccount(Guid.NewGuid(), tenantId, customerId);
    }

    /// <summary>
    /// Credita pontos por um atendimento concluído.
    /// </summary>
    public void Earn(int points)
    {
        if (points <= 0)
        {
            throw new DomainRuleException("A quantidade de pontos deve ser maior que zero.");
        }

        Points += points;
        TotalEarned += points;
    }

    /// <summary>
    /// Resgata pontos. Falha se o saldo for insuficiente.
    /// </summary>
    public void Redeem(int points)
    {
        if (points <= 0)
        {
            throw new DomainRuleException("A quantidade de pontos deve ser maior que zero.");
        }

        if (points > Points)
        {
            throw new DomainRuleException(
                $"Saldo insuficiente: o cliente tem {Points} pontos.");
        }

        Points -= points;
        TotalRedeemed += points;
    }

    /// <summary>Indica se o cliente pode resgatar a quantidade informada.</summary>
    public bool CanRedeem(int points) => points > 0 && points <= Points;
}
