using Servia.Domain.Common;

namespace Servia.Domain.Operations;

/// <summary>
/// Política de retenção de dados (F9.5).
///
/// Define por quanto tempo cada categoria é mantida antes de ser removida.
/// O prazo não é arbitrário: dados financeiros têm exigência legal de guarda,
/// enquanto dados de navegação podem ser descartados antes.
///
/// A avaliação é uma função pura: dada a data do registro e a data atual,
/// o registro está ou não expirado.
/// </summary>
public sealed class RetentionPolicy
{
    private readonly Dictionary<DataCategory, int> _retentionDays = [];

    private RetentionPolicy(string name)
    {
        Name = name;
    }

    public string Name { get; }

    /// <summary>Prazo de retenção por categoria, em dias.</summary>
    public IReadOnlyDictionary<DataCategory, int> RetentionDays => _retentionDays;

    /// <summary>
    /// Cria a política padrão da plataforma.
    ///
    /// Os prazos refletem as obrigações reais: financeiro é mantido por 5
    /// anos (prazo fiscal), auditoria por 2 anos, e dados operacionais por
    /// 1 ano.
    /// </summary>
    public static RetentionPolicy CreateDefault()
    {
        var policy = new RetentionPolicy("Padrão da plataforma");

        policy.SetRetention(DataCategory.Financial, 5 * 365);
        policy.SetRetention(DataCategory.Audit, 2 * 365);
        policy.SetRetention(DataCategory.Operational, 365);
        policy.SetRetention(DataCategory.Messaging, 180);
        policy.SetRetention(DataCategory.AiLogs, 90);

        return policy;
    }

    /// <summary>Define o prazo de retenção de uma categoria.</summary>
    public void SetRetention(DataCategory category, int days)
    {
        if (days <= 0)
        {
            throw new DomainRuleException("O prazo de retenção deve ser maior que zero.");
        }

        _retentionDays[category] = days;
    }

    /// <summary>Retorna o prazo de uma categoria, ou nulo se não definido.</summary>
    public int? RetentionFor(DataCategory category) =>
        _retentionDays.TryGetValue(category, out var days) ? days : null;

    /// <summary>
    /// Indica se um registro está expirado e pode ser removido.
    /// Categoria sem prazo definido nunca expira.
    /// </summary>
    public bool IsExpired(DataCategory category, DateOnly recordDate, DateOnly today)
    {
        var days = RetentionFor(category);

        if (days is null)
        {
            return false;
        }

        return today.DayNumber - recordDate.DayNumber > days.Value;
    }
}

/// <summary>Categoria de dado, para fins de retenção.</summary>
public enum DataCategory
{
    /// <summary>Lançamentos, pagamentos e comissões.</summary>
    Financial = 1,

    /// <summary>Trilha de auditoria.</summary>
    Audit = 2,

    /// <summary>Agendamentos, clientes e catálogo.</summary>
    Operational = 3,

    /// <summary>Conversas e notificações.</summary>
    Messaging = 4,

    /// <summary>Logs de operação de IA.</summary>
    AiLogs = 5
}
