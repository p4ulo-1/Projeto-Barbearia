using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Loyalty;

/// <summary>
/// Avaliação de um atendimento concluído (F7.7).
///
/// Coleta nota de 0 a 10 e comentário opcional. A nota alimenta o NPS:
/// promotores (9–10), neutros (7–8) e detratores (0–6).
///
/// A avaliação é vinculada ao atendimento: um atendimento tem no máximo uma
/// avaliação, evitando distorcer o indicador.
/// </summary>
public sealed class ServiceReview : ITenantScoped
{
    private ServiceReview(
        Guid id,
        Guid tenantId,
        Guid appointmentId,
        Guid customerId,
        int score,
        string? comment,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        AppointmentId = appointmentId;
        CustomerId = customerId;
        Score = score;
        Comment = comment;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>.
    /// </summary>
    private ServiceReview()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid AppointmentId { get; private set; }

    public Guid CustomerId { get; private set; }

    /// <summary>Nota de 0 a 10.</summary>
    public int Score { get; private set; }

    public string? Comment { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Classificação NPS da nota.</summary>
    public NpsCategory Category => ServiceReview.Classify(Score);

    public static ServiceReview Create(
        Guid tenantId,
        Guid appointmentId,
        Guid customerId,
        int score,
        DateTimeOffset now,
        string? comment = null)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (appointmentId == Guid.Empty)
        {
            throw new DomainRuleException("O atendimento é obrigatório.");
        }

        if (customerId == Guid.Empty)
        {
            throw new DomainRuleException("O cliente é obrigatório.");
        }

        if (score is < 0 or > 10)
        {
            throw new DomainRuleException("A nota deve estar entre 0 e 10.");
        }

        return new ServiceReview(
            Guid.NewGuid(), tenantId, appointmentId, customerId, score,
            string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(), now);
    }

    /// <summary>Classifica uma nota em promotor, neutro ou detrator.</summary>
    public static NpsCategory Classify(int score)
    {
        if (score is < 0 or > 10)
        {
            throw new DomainRuleException("A nota deve estar entre 0 e 10.");
        }

        return score switch
        {
            >= 9 => NpsCategory.Promoter,
            >= 7 => NpsCategory.Passive,
            _ => NpsCategory.Detractor
        };
    }
}

/// <summary>Classificação NPS de uma avaliação.</summary>
public enum NpsCategory
{
    /// <summary>Nota 0–6: cliente insatisfeito.</summary>
    Detractor = 1,

    /// <summary>Nota 7–8: cliente neutro.</summary>
    Passive = 2,

    /// <summary>Nota 9–10: cliente promotor.</summary>
    Promoter = 3
}

/// <summary>
/// Resultado agregado de NPS de um período (F7.7).
/// </summary>
public sealed record NpsReport(
    int TotalResponses,
    int Promoters,
    int Passives,
    int Detractors)
{
    /// <summary>
    /// NPS: percentual de promotores menos percentual de detratores.
    /// Varia de -100 a +100. Zero quando não há respostas.
    /// </summary>
    public decimal Score =>
        TotalResponses == 0
            ? 0m
            : Math.Round(
                (Promoters * 100m / TotalResponses) - (Detractors * 100m / TotalResponses),
                1,
                MidpointRounding.AwayFromZero);

    /// <summary>Calcula o NPS a partir das avaliações do período.</summary>
    public static NpsReport Build(IEnumerable<ServiceReview> reviews)
    {
        ArgumentNullException.ThrowIfNull(reviews);

        var list = reviews.ToList();

        return new NpsReport(
            list.Count,
            list.Count(review => review.Category == NpsCategory.Promoter),
            list.Count(review => review.Category == NpsCategory.Passive),
            list.Count(review => review.Category == NpsCategory.Detractor));
    }
}
