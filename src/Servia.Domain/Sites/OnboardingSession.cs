using Servia.Domain.Ai;
using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Domain.Sites;

/// <summary>
/// Sessão de onboarding assistido por IA (item 1.2).
///
/// Conduz a conversa que reúne as informações do estabelecimento e
/// produz a configuração do site. O histórico é persistido: o proprietário
/// pode sair e voltar sem perder o que já respondeu.
///
/// <b>Estados:</b> a sessão percorre configuração → revisão → aprovação →
/// publicado. Nenhuma transição é automática: publicar exige aprovação
/// explícita do proprietário.
/// </summary>
public sealed class OnboardingSession : ITenantScoped
{
    private readonly List<OnboardingMessage> _messages = [];
    private readonly Dictionary<string, string> _collectedFacts = [];

    private OnboardingSession(
        Guid id,
        Guid tenantId,
        string businessName,
        SegmentType segment,
        DateTimeOffset startedAt)
    {
        Id = id;
        TenantId = tenantId;
        BusinessName = businessName;
        Segment = segment;
        StartedAt = startedAt;
        Status = OnboardingStatus.Collecting;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre em <see cref="Start"/>.
    /// </summary>
    private OnboardingSession()
    {
        BusinessName = string.Empty;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string BusinessName { get; private set; }

    public SegmentType Segment { get; private set; }

    public OnboardingStatus Status { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset? LastActivityAt { get; private set; }

    /// <summary>Histórico completo da conversa, em ordem cronológica.</summary>
    public IReadOnlyCollection<OnboardingMessage> Messages => _messages;

    /// <summary>Informações já extraídas das respostas do proprietário.</summary>
    public IReadOnlyDictionary<string, string> CollectedFacts => _collectedFacts;

    /// <summary>Configuração gerada pela IA, quando existir.</summary>
    public Guid? GeneratedChangeSetId { get; private set; }

    /// <summary>Indica se a sessão ainda aceita mensagens.</summary>
    public bool IsOpen => Status is OnboardingStatus.Collecting or OnboardingStatus.Reviewing;

    /// <summary>
    /// Inicia uma sessão de onboarding.
    /// </summary>
    public static OnboardingSession Start(
        Guid tenantId,
        string businessName,
        SegmentType segment,
        DateTimeOffset now)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainRuleException("O estabelecimento é obrigatório.");
        }

        if (string.IsNullOrWhiteSpace(businessName))
        {
            throw new DomainRuleException("O nome do estabelecimento é obrigatório.");
        }

        return new OnboardingSession(
            Guid.NewGuid(), tenantId, businessName.Trim(), segment, now);
    }

    /// <summary>
    /// Registra a pergunta da IA.
    /// </summary>
    public OnboardingMessage AskQuestion(string question, DateTimeOffset now)
    {
        EnsureOpen();

        if (string.IsNullOrWhiteSpace(question))
        {
            throw new DomainRuleException("A pergunta não pode ser vazia.");
        }

        var message = OnboardingMessage.FromAssistant(question.Trim(), now);
        _messages.Add(message);
        LastActivityAt = now;

        return message;
    }

    /// <summary>
    /// Registra a resposta do proprietário e as informações extraídas.
    /// </summary>
    public OnboardingMessage RecordAnswer(
        string answer,
        IReadOnlyDictionary<string, string> extractedFacts,
        DateTimeOffset now)
    {
        EnsureOpen();

        if (string.IsNullOrWhiteSpace(answer))
        {
            throw new DomainRuleException("A resposta não pode ser vazia.");
        }

        ArgumentNullException.ThrowIfNull(extractedFacts);

        var message = OnboardingMessage.FromOwner(answer.Trim(), now);
        _messages.Add(message);

        // Fatos extraidos complementam o que ja existe; nao sobrescrevem
        // com vazio, para nao perder informacao ja fornecida.
        foreach (var fact in extractedFacts)
        {
            if (!string.IsNullOrWhiteSpace(fact.Value))
            {
                _collectedFacts[fact.Key] = fact.Value.Trim();
            }
        }

        LastActivityAt = now;

        return message;
    }

    /// <summary>
    /// Anexa informações extraídas pela IA à última resposta registrada.
    ///
    /// Separado de <see cref="RecordAnswer"/> para não duplicar a mensagem
    /// no histórico: a resposta do proprietário é registrada uma única vez,
    /// e os fatos que a IA extraiu dela chegam depois.
    /// </summary>
    public void RecordFacts(IReadOnlyDictionary<string, string> extractedFacts, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(extractedFacts);

        foreach (var fact in extractedFacts)
        {
            if (string.IsNullOrWhiteSpace(fact.Key) || string.IsNullOrWhiteSpace(fact.Value))
            {
                continue;
            }

            // Fatos complementam o que ja existe; nao sobrescrevem com vazio.
            _collectedFacts[fact.Key.Trim()] = fact.Value.Trim();
        }

        LastActivityAt = now;
    }

    /// <summary>
    /// Registra a configuração gerada pela IA e move para revisão.
    /// </summary>
    public void AttachGeneratedConfiguration(Guid changeSetId, DateTimeOffset now)
    {
        if (changeSetId == Guid.Empty)
        {
            throw new DomainRuleException("A configuração gerada é obrigatória.");
        }

        if (Status != OnboardingStatus.Collecting)
        {
            throw new DomainRuleException(
                "Só é possível gerar configuração durante a coleta de informações.");
        }

        GeneratedChangeSetId = changeSetId;
        Status = OnboardingStatus.Reviewing;
        LastActivityAt = now;
    }

    /// <summary>
    /// Marca a configuração como pronta para aprovação.
    /// </summary>
    public void SubmitForApproval(DateTimeOffset now)
    {
        if (Status != OnboardingStatus.Reviewing)
        {
            throw new DomainRuleException(
                "Só é possível enviar para aprovação uma configuração em revisão.");
        }

        if (GeneratedChangeSetId is null)
        {
            throw new DomainRuleException(
                "Não há configuração gerada para enviar à aprovação.");
        }

        Status = OnboardingStatus.AwaitingApproval;
        LastActivityAt = now;
    }

    /// <summary>
    /// Registra a aprovação explícita do proprietário.
    /// </summary>
    public void Approve(Guid approvedBy, DateTimeOffset now)
    {
        if (Status != OnboardingStatus.AwaitingApproval)
        {
            throw new DomainRuleException(
                "Só é possível aprovar uma configuração aguardando aprovação.");
        }

        if (approvedBy == Guid.Empty)
        {
            throw new DomainRuleException("O responsável pela aprovação é obrigatório.");
        }

        Status = OnboardingStatus.Approved;
        ApprovedBy = approvedBy;
        ApprovedAt = now;
        LastActivityAt = now;
    }

    /// <summary>
    /// Registra a publicação do site.
    /// </summary>
    public void MarkPublished(DateTimeOffset now)
    {
        if (Status != OnboardingStatus.Approved)
        {
            throw new DomainRuleException(
                "Somente uma configuração aprovada pode ser publicada.");
        }

        Status = OnboardingStatus.Published;
        LastActivityAt = now;
    }

    /// <summary>
    /// Volta para revisão quando o proprietário pede ajustes.
    /// </summary>
    public void RequestAdjustments(DateTimeOffset now)
    {
        if (Status is not (OnboardingStatus.AwaitingApproval or OnboardingStatus.Reviewing))
        {
            throw new DomainRuleException(
                "Só é possível pedir ajustes antes da aprovação.");
        }

        Status = OnboardingStatus.Reviewing;
        ApprovedBy = null;
        ApprovedAt = null;
        LastActivityAt = now;
    }

    public Guid? ApprovedBy { get; private set; }

    public DateTimeOffset? ApprovedAt { get; private set; }

    private void EnsureOpen()
    {
        if (!IsOpen)
        {
            throw new DomainRuleException(
                $"A sessão está em '{Status}' e não aceita novas mensagens.");
        }
    }
}

/// <summary>Estado da sessão de onboarding.</summary>
public enum OnboardingStatus
{
    /// <summary>Coletando informações por conversa.</summary>
    Collecting = 1,

    /// <summary>Configuração gerada, em revisão pelo proprietário.</summary>
    Reviewing = 2,

    /// <summary>Aguardando aprovação explícita.</summary>
    AwaitingApproval = 3,

    /// <summary>Aprovada, pronta para publicar.</summary>
    Approved = 4,

    /// <summary>Publicada.</summary>
    Published = 5
}

/// <summary>Mensagem do histórico de onboarding.</summary>
public sealed class OnboardingMessage
{
    private OnboardingMessage(Guid id, AiConversationRole role, string content, DateTimeOffset sentAt)
    {
        Id = id;
        Role = role;
        Content = content;
        SentAt = sentAt;
    }

    /// <summary>
    /// Construtor sem parâmetros exigido pelo EF Core para materializar a
    /// entidade. Não valida invariantes: a validação ocorre nas fábricas.
    /// </summary>
    private OnboardingMessage()
    {
        Content = string.Empty;
    }

    public Guid Id { get; private set; }

    public AiConversationRole Role { get; private set; }

    public string Content { get; private set; }

    public DateTimeOffset SentAt { get; private set; }

    internal static OnboardingMessage FromAssistant(string content, DateTimeOffset now) =>
        new(Guid.NewGuid(), AiConversationRole.Assistant, content, now);

    internal static OnboardingMessage FromOwner(string content, DateTimeOffset now) =>
        new(Guid.NewGuid(), AiConversationRole.Owner, content, now);
}
