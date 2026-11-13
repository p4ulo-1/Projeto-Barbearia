using Servia.Domain.Common;
using Servia.Domain.Sites;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Ai;

/// <summary>
/// Alteração direcionada do site proposta pela IA (C12).
///
/// Diferente de <see cref="AiChangeSet"/> (que propõe o site inteiro na
/// geração inicial), esta alteração é <b>cirúrgica</b>: cada item identifica
/// a página, o componente e o campo a mudar, com o valor atual e o proposto.
/// Só o que foi aprovado é aplicado; páginas e elementos fora da solicitação
/// permanecem intactos.
///
/// <b>Rastreabilidade:</b> guarda a solicitação original do cliente.
///
/// <b>Aprovação:</b> a alteração nasce <c>Proposed</c>; só é aplicada após
/// <see cref="Approve"/>. Rejeitar encerra sem aplicar nada.
///
/// <b>Isolamento:</b> pertence a um tenant; nunca é lida por outro.
/// </summary>
public sealed class AiSiteChange : ITenantScoped
{
    private readonly List<AiPageChange> _pages = [];

    private AiSiteChange(Guid id, Guid tenantId, string request, DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        Request = request;
        CreatedAt = createdAt;
        Status = AiSiteChangeStatus.Proposed;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Create"/>.
    /// </summary>
    private AiSiteChange()
    {
        Request = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>Solicitação original do cliente, preservada para rastreabilidade.</summary>
    public string Request { get; private set; }

    public AiSiteChangeStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    public IReadOnlyCollection<AiPageChange> Pages => _pages;

    public static AiSiteChange Create(Guid tenantId, string request, DateTimeOffset now)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(request))
        {
            throw new DomainRuleException("A solicitação de alteração é obrigatória.");
        }

        return new AiSiteChange(Guid.NewGuid(), tenantId, request.Trim(), now);
    }

    /// <summary>
    /// Adiciona uma alteração a uma página. A página é identificada pelo slug
    /// (vazio = página inicial).
    /// </summary>
    public AiPageChange AddPageChange(string pageSlug)
    {
        if (Status != AiSiteChangeStatus.Proposed)
        {
            throw new DomainRuleException("Só é possível alterar uma proposta pendente.");
        }

        var normalized = (pageSlug ?? string.Empty).Trim().ToLowerInvariant();

        var existing = _pages.FirstOrDefault(page => page.PageSlug == normalized);

        if (existing is not null)
        {
            return existing;
        }

        var page = new AiPageChange(normalized);
        _pages.Add(page);

        return page;
    }

    /// <summary>Indica se a proposta tem ao menos uma alteração de campo.</summary>
    public bool HasChanges() => _pages.Any(page => page.Fields.Count > 0);

    /// <summary>
    /// Valida a estrutura da proposta antes de aplicar: cada campo precisa de
    /// componente válido, chave permitida pelo schema e valor proposto.
    /// </summary>
    public void EnsureValid()
    {
        if (!HasChanges())
        {
            throw new DomainRuleException("A proposta não contém alterações.");
        }

        foreach (var page in _pages)
        {
            foreach (var field in page.Fields)
            {
                if (!SiteComponentSchema.IsSupported(field.ComponentType))
                {
                    throw new DomainRuleException(
                        $"O componente {field.ComponentType} não faz parte da biblioteca controlada.");
                }

                var allowed = SiteComponentSchema.AllowedKeys(field.ComponentType);

                if (!allowed.Contains(field.FieldKey, StringComparer.OrdinalIgnoreCase))
                {
                    throw new DomainRuleException(
                        $"A propriedade '{field.FieldKey}' não é válida para o componente {field.ComponentType}.");
                }

                if (string.IsNullOrWhiteSpace(field.ProposedValue))
                {
                    throw new DomainRuleException(
                        $"O valor proposto para '{field.FieldKey}' é obrigatório.");
                }
            }
        }
    }

    /// <summary>Registra a aprovação explícita do cliente.</summary>
    public void Approve(DateTimeOffset now)
    {
        if (Status != AiSiteChangeStatus.Proposed)
        {
            throw new DomainRuleException("Só é possível aprovar uma proposta pendente.");
        }

        EnsureValid();

        Status = AiSiteChangeStatus.Approved;
        DecidedAt = now;
    }

    /// <summary>Registra a rejeição explícita do cliente.</summary>
    public void Reject(DateTimeOffset now)
    {
        if (Status != AiSiteChangeStatus.Proposed)
        {
            throw new DomainRuleException("Só é possível rejeitar uma proposta pendente.");
        }

        Status = AiSiteChangeStatus.Rejected;
        DecidedAt = now;
    }

    /// <summary>Marca a proposta aprovada como aplicada ao site.</summary>
    public void MarkApplied(DateTimeOffset now)
    {
        if (Status != AiSiteChangeStatus.Approved)
        {
            throw new DomainRuleException("Só uma proposta aprovada pode ser aplicada.");
        }

        Status = AiSiteChangeStatus.Applied;
        DecidedAt = now;
    }
}

/// <summary>Estado da alteração direcionada.</summary>
public enum AiSiteChangeStatus
{
    /// <summary>Aguardando decisão do cliente.</summary>
    Proposed = 1,

    /// <summary>Aprovada, pronta para aplicar.</summary>
    Approved = 2,

    /// <summary>Rejeitada; nada é aplicado.</summary>
    Rejected = 3,

    /// <summary>Aplicada ao site.</summary>
    Applied = 4
}

/// <summary>Alterações de uma página.</summary>
public sealed class AiPageChange
{
    private readonly List<AiFieldChange> _fields = [];

    internal AiPageChange(string pageSlug)
    {
        PageSlug = pageSlug;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em
    /// <see cref="AiSiteChange.AddPageChange"/>.
    /// </summary>
    private AiPageChange()
    {
        PageSlug = string.Empty;
    }

    /// <summary>Slug da página alvo. Vazio = página inicial.</summary>
    public string PageSlug { get; private set; }

    public IReadOnlyCollection<AiFieldChange> Fields => _fields;

    /// <summary>
    /// Adiciona a alteração de um campo. O componente identifica a seção na
    /// página; a chave identifica a propriedade dentro do componente.
    /// </summary>
    public AiFieldChange AddFieldChange(
        SiteComponentType componentType,
        string fieldKey,
        string? currentValue,
        string proposedValue)
    {
        if (string.IsNullOrWhiteSpace(fieldKey))
        {
            throw new DomainRuleException("A chave do campo é obrigatória.");
        }

        var field = new AiFieldChange(
            componentType,
            fieldKey.Trim(),
            currentValue,
            proposedValue);

        _fields.Add(field);

        return field;
    }
}

/// <summary>Alteração de um campo de um componente.</summary>
public sealed class AiFieldChange
{
    internal AiFieldChange(
        SiteComponentType componentType,
        string fieldKey,
        string? currentValue,
        string proposedValue)
    {
        ComponentType = componentType;
        FieldKey = fieldKey;
        CurrentValue = currentValue;
        ProposedValue = proposedValue;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em
    /// <see cref="AiPageChange.AddFieldChange"/>.
    /// </summary>
    private AiFieldChange()
    {
        FieldKey = string.Empty;
        ProposedValue = string.Empty;
    }

    public SiteComponentType ComponentType { get; private set; }

    public string FieldKey { get; private set; }

    /// <summary>Valor atual no site, para o cliente comparar.</summary>
    public string? CurrentValue { get; private set; }

    /// <summary>Valor proposto pela IA.</summary>
    public string ProposedValue { get; private set; }
}
