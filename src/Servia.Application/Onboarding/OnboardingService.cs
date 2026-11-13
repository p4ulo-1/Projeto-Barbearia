using Servia.Domain.Ai;
using Servia.Domain.Common;
using Servia.Domain.Sites;
using Servia.Domain.Tenancy;

namespace Servia.Application.Onboarding;

/// <summary>
/// Casos de uso do onboarding assistido por IA (itens 1 e 2).
///
/// Conduz a conversa, gera a configuração, controla os estados e publica.
/// A lógica vive aqui, não no controller: pode ser testada sem subir a API.
///
/// <b>Regra de ouro:</b> a IA propõe, o proprietário aprova, o sistema
/// publica. Nenhuma etapa é automática — publicar sem aprovação é
/// rejeitado pelo domínio.
///
/// <b>Independência de fornecedor:</b> a conversa e a geração dependem dos
/// contratos do domínio (<see cref="IAiConversationProvider"/>,
/// <see cref="IAiSiteGenerator"/>). Trocar de provedor não altera regra de
/// negócio — só a composição em <c>Program.cs</c>.
/// </summary>
public sealed class OnboardingService(
    IOnboardingRepository repository,
    IAiConversationProvider conversation,
    IAiSiteGenerator generator,
    TimeProvider timeProvider,
    Servia.Application.Billing.AiUsageService aiUsage)
{
    /// <summary>
    /// Informações mínimas que a conversa deve reunir antes da geração.
    /// </summary>
    private static readonly string[] RequiredFacts =
    [
        "description", "address", "phone", "openingHours", "services", "professionals"
    ];

    /// <summary>
    /// Inicia uma sessão de onboarding (item 4).
    ///
    /// Se já existe uma sessão em andamento, ela é retomada em vez de criar
    /// outra: o proprietário pode ter saído no meio e não deve perder o que
    /// já respondeu.
    /// </summary>
    public async Task<OnboardingResult> StartAsync(
        Guid tenantId,
        string businessName,
        SegmentType segment,
        CancellationToken cancellationToken = default)
    {
        var existing = await repository.FindOpenSessionAsync(tenantId, cancellationToken);

        if (existing is not null)
        {
            return OnboardingResult.Resumed(existing);
        }

        OnboardingSession session;

        try
        {
            session = OnboardingSession.Start(
                tenantId, businessName, segment, timeProvider.GetUtcNow());
        }
        catch (DomainRuleException exception)
        {
            return OnboardingResult.Invalid(exception.Message);
        }

        await repository.AddSessionAsync(session, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return OnboardingResult.Started(session);
    }

    /// <summary>
    /// Envia uma mensagem do proprietário e recebe a próxima pergunta (item 1.2).
    ///
    /// A resposta é registrada <b>antes</b> da chamada à IA: se a chamada
    /// falhar, o que o proprietário escreveu não se perde. Os fatos extraídos
    /// pela IA complementam a mesma mensagem, sem duplicá-la no histórico.
    /// </summary>
    public async Task<OnboardingResult> SendMessageAsync(
        Guid tenantId,
        Guid sessionId,
        string message,
        CancellationToken cancellationToken = default)
    {
        var session = await LoadSessionAsync(tenantId, sessionId, cancellationToken);

        if (session is null)
        {
            return OnboardingResult.NotFound("Sessão de onboarding não encontrada.");
        }

        if (!session.IsOpen)
        {
            return OnboardingResult.Invalid(
                $"A sessão está em '{session.Status}' e não aceita novas mensagens.");
        }

        var now = timeProvider.GetUtcNow();

        try
        {
            // Registra a resposta uma única vez. Os fatos são anexados depois,
            // na mesma mensagem, via RecordFacts.
            session.RecordAnswer(message, new Dictionary<string, string>(), now);
        }
        catch (DomainRuleException exception)
        {
            return OnboardingResult.Invalid(exception.Message);
        }

        var context = new AiConversationContext(
            session.BusinessName,
            session.Segment.ToString(),
            session.Messages
                .OrderBy(item => item.SentAt)
                .Select(item => new AiConversationMessage(item.Role, item.Content, item.SentAt))
                .ToList(),
            session.CollectedFacts,
            MissingFacts(session));

        var turn = await conversation.ContinueAsync(context, cancellationToken);

        if (!turn.Success)
        {
            // A resposta do proprietário já está persistida; devolvemos o
            // motivo para o frontend exibir e permitir nova tentativa.
            await repository.SaveChangesAsync(cancellationToken);
            return OnboardingResult.Invalid(turn.FailureReason ?? "A IA não respondeu.");
        }

        if (turn.ExtractedFacts.Count > 0)
        {
            session.RecordFacts(turn.ExtractedFacts, now);
        }

        if (turn.Question is not null)
        {
            session.AskQuestion(turn.Question, now);
        }

        await repository.SaveChangesAsync(cancellationToken);

        return OnboardingResult.Conversation(session, turn.Question, turn.IsComplete);
    }

    /// <summary>
    /// Gera a configuração do site a partir das informações coletadas (item 1.4).
    /// </summary>
    public async Task<OnboardingResult> GenerateConfigurationAsync(
        Guid tenantId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await LoadSessionAsync(tenantId, sessionId, cancellationToken);

        if (session is null)
        {
            return OnboardingResult.NotFound("Sessão de onboarding não encontrada.");
        }

        if (session.Status != OnboardingStatus.Collecting)
        {
            return OnboardingResult.Invalid(
                $"A configuração só pode ser gerada durante a coleta. Estado atual: '{session.Status}'.");
        }

        // Política provisória: bloqueia quando a franquia de IA está esgotada.
        await aiUsage.EnsureAvailableAsync(tenantId, cancellationToken);

        var result = await generator.GenerateAsync(BuildRequest(session), cancellationToken);

        if (!result.Success || result.ChangeSet is null)
        {
            return OnboardingResult.Invalid(result.FailureReason ?? "A geração falhou.");
        }

        // Registra o consumo real (tokens). Sem cobrança de excedente.
        await aiUsage.RecordConsumptionAsync(tenantId, result.UnitsConsumed, cancellationToken);

        // Guardrail: a proposta precisa estar completa antes de ser aceita.
        // O AiChangeSet já rejeitou componente/propriedade fora do schema;
        // aqui garantimos que as propriedades obrigatórias estão preenchidas.
        if (!result.ChangeSet.IsComplete())
        {
            return OnboardingResult.Invalid(
                "A configuração gerada está incompleta e não pode ser usada.");
        }

        await repository.AddChangeSetAsync(result.ChangeSet, cancellationToken);

        session.AttachGeneratedConfiguration(result.ChangeSet.Id, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);

        return OnboardingResult.Generated(session, result.ChangeSet);
    }

    /// <summary>
    /// Solicita ajustes à IA sobre a configuração atual (item 2.1).
    ///
    /// Preserva o histórico: cada ajuste cria uma nova proposta, e a anterior
    /// permanece registrada. A conversa continua disponível.
    /// </summary>
    public async Task<OnboardingResult> RequestAdjustmentsAsync(
        Guid tenantId,
        Guid sessionId,
        string instruction,
        CancellationToken cancellationToken = default)
    {
        var session = await LoadSessionAsync(tenantId, sessionId, cancellationToken);

        if (session is null)
        {
            return OnboardingResult.NotFound("Sessão de onboarding não encontrada.");
        }

        if (session.GeneratedChangeSetId is null)
        {
            return OnboardingResult.Invalid("Não há configuração gerada para ajustar.");
        }

        if (string.IsNullOrWhiteSpace(instruction))
        {
            return OnboardingResult.Invalid("A instrução de ajuste é obrigatória.");
        }

        var current = await repository.FindChangeSetAsync(
            tenantId, session.GeneratedChangeSetId.Value, cancellationToken);

        if (current is null)
        {
            return OnboardingResult.Invalid("A configuração atual não foi encontrada.");
        }

        // Política provisória: bloqueia quando a franquia de IA está esgotada.
        await aiUsage.EnsureAvailableAsync(tenantId, cancellationToken);

        var result = await generator.RefineAsync(
            BuildRequest(session), current, instruction, cancellationToken);

        if (!result.Success || result.ChangeSet is null)
        {
            return OnboardingResult.Invalid(result.FailureReason ?? "O ajuste falhou.");
        }

        // Registra o consumo real (tokens). Sem cobrança de excedente.
        await aiUsage.RecordConsumptionAsync(tenantId, result.UnitsConsumed, cancellationToken);

        if (!result.ChangeSet.IsComplete())
        {
            return OnboardingResult.Invalid(
                "A configuração ajustada está incompleta e não pode ser usada.");
        }

        await repository.AddChangeSetAsync(result.ChangeSet, cancellationToken);

        session.AttachGeneratedConfiguration(result.ChangeSet.Id, timeProvider.GetUtcNow());
        session.RequestAdjustments(timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);

        return OnboardingResult.Generated(session, result.ChangeSet);
    }

    /// <summary>
    /// Envia a configuração para aprovação (item 2.2).
    /// </summary>
    public async Task<OnboardingResult> SubmitForApprovalAsync(
        Guid tenantId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await LoadSessionAsync(tenantId, sessionId, cancellationToken);

        if (session is null)
        {
            return OnboardingResult.NotFound("Sessão de onboarding não encontrada.");
        }

        try
        {
            session.SubmitForApproval(timeProvider.GetUtcNow());
        }
        catch (DomainRuleException exception)
        {
            return OnboardingResult.Invalid(exception.Message);
        }

        await repository.SaveChangesAsync(cancellationToken);

        return OnboardingResult.Updated(session);
    }

    /// <summary>
    /// Registra a aprovação explícita do proprietário (item 2.2).
    /// </summary>
    public async Task<OnboardingResult> ApproveAsync(
        Guid tenantId,
        Guid sessionId,
        Guid approvedBy,
        CancellationToken cancellationToken = default)
    {
        var session = await LoadSessionAsync(tenantId, sessionId, cancellationToken);

        if (session is null)
        {
            return OnboardingResult.NotFound("Sessão de onboarding não encontrada.");
        }

        try
        {
            session.Approve(approvedBy, timeProvider.GetUtcNow());
        }
        catch (DomainRuleException exception)
        {
            return OnboardingResult.Invalid(exception.Message);
        }

        await repository.SaveChangesAsync(cancellationToken);

        return OnboardingResult.Updated(session);
    }

    /// <summary>
    /// Publica o site a partir da configuração aprovada (item 2.3).
    ///
    /// <b>Somente configuração aprovada é publicada.</b> O domínio rejeita
    /// qualquer outro estado — não há caminho alternativo.
    ///
    /// Se o tenant ainda não tem site, um é criado a partir do slug do
    /// estabelecimento: o fluxo de onboarding conclui com o site no ar.
    /// </summary>
    public async Task<OnboardingResult> PublishAsync(
        Guid tenantId,
        Guid sessionId,
        Guid publishedBy,
        string subdomain,
        CancellationToken cancellationToken = default)
    {
        var session = await LoadSessionAsync(tenantId, sessionId, cancellationToken);

        if (session is null)
        {
            return OnboardingResult.NotFound("Sessão de onboarding não encontrada.");
        }

        if (session.Status != OnboardingStatus.Approved)
        {
            return OnboardingResult.Invalid(
                "Somente uma configuração aprovada pode ser publicada. "
                + $"Estado atual: '{session.Status}'.");
        }

        if (session.GeneratedChangeSetId is null)
        {
            return OnboardingResult.Invalid("Não há configuração para publicar.");
        }

        var changeSet = await repository.FindChangeSetAsync(
            tenantId, session.GeneratedChangeSetId.Value, cancellationToken);

        if (changeSet is null)
        {
            return OnboardingResult.Invalid("A configuração aprovada não foi encontrada.");
        }

        var site = await repository.FindSiteAsync(tenantId, cancellationToken);

        if (site is null)
        {
            try
            {
                site = Site.Create(tenantId, subdomain);
            }
            catch (DomainRuleException exception)
            {
                return OnboardingResult.Invalid(exception.Message);
            }

            await repository.AddSiteAsync(site, cancellationToken);
        }

        var now = timeProvider.GetUtcNow();

        try
        {
            ApplyChangeSet(site, changeSet);

            var publication = site.Publish(publishedBy, now);

            session.MarkPublished(now);
            await repository.SaveChangesAsync(cancellationToken);

            return OnboardingResult.Published(session, site, publication);
        }
        catch (DomainRuleException exception)
        {
            return OnboardingResult.Invalid(exception.Message);
        }
        catch (ConcurrencyConflictException exception)
        {
            // C10: outra publicacao concorrente venceu a disputa.
            return OnboardingResult.Conflict(exception.Message);
        }
    }

    /// <summary>
    /// Retoma uma sessão existente (item 1.2).
    /// </summary>
    public async Task<OnboardingResult> ResumeAsync(
        Guid tenantId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await LoadSessionAsync(tenantId, sessionId, cancellationToken);

        return session is null
            ? OnboardingResult.NotFound("Sessão de onboarding não encontrada.")
            : OnboardingResult.Resumed(session);
    }

    /// <summary>
    /// Lista as sessões do tenant.
    /// </summary>
    public async Task<IReadOnlyList<OnboardingSession>> ListAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default) =>
        await repository.ListSessionsAsync(tenantId, cancellationToken);

    private async Task<OnboardingSession?> LoadSessionAsync(
        Guid tenantId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || sessionId == Guid.Empty)
        {
            return null;
        }

        return await repository.FindSessionAsync(tenantId, sessionId, cancellationToken);
    }

    /// <summary>
    /// Informações que a IA ainda precisa obter.
    /// </summary>
    private static IReadOnlyList<string> MissingFacts(OnboardingSession session) =>
        RequiredFacts
            .Where(fact => !session.CollectedFacts.ContainsKey(fact))
            .ToList();

    private static AiGenerationRequest BuildRequest(OnboardingSession session) =>
        AiGenerationRequest.Create(
            session.TenantId,
            session.BusinessName,
            session.Segment,
            SiteCustomizationLevel.Basic,
            preferences: DescribeFacts(session));

    /// <summary>
    /// Descreve os fatos coletados para a IA, sem inventar informação.
    /// </summary>
    private static string DescribeFacts(OnboardingSession session) =>
        string.Join(
            "; ",
            session.CollectedFacts.Select(fact => $"{fact.Key}: {fact.Value}"));

    /// <summary>
    /// Aplica a configuração aprovada ao site.
    ///
    /// Só usa componentes da biblioteca controlada: a proposta já foi
    /// validada por <see cref="AiChangeSet"/>, que rejeita componente ou
    /// propriedade fora do schema. A página inicial é limpa antes de aplicar,
    /// para que republicações não dupliquem seções.
    /// </summary>
    private static void ApplyChangeSet(Site site, AiChangeSet changeSet)
    {
        site.ChangeTheme(changeSet.Theme);

        var page = site.Pages.FirstOrDefault();

        if (page is null)
        {
            page = site.AddPage(string.Empty, "Início");
        }

        // Remove seções do rascunho anterior para aplicar a versão aprovada.
        foreach (var existing in page.Sections.ToList())
        {
            page.RemoveSection(existing.Id);
        }

        foreach (var section in changeSet.Sections.OrderBy(item => item.SortOrder))
        {
            var created = page.AddSection(section.ComponentType);

            foreach (var property in section.Configuration)
            {
                created.SetProperty(property.Key, property.Value);
            }
        }
    }
}

/// <summary>Resultado de uma operação de onboarding.</summary>
public sealed record OnboardingResult(
    OnboardingOutcome Outcome,
    OnboardingSession? Session,
    AiChangeSet? ChangeSet,
    Site? Site,
    SitePublication? Publication,
    string? Question,
    bool IsConversationComplete,
    string? FailureReason)
{
    public static OnboardingResult Started(OnboardingSession session) =>
        new(OnboardingOutcome.Started, session, null, null, null, null, false, null);

    public static OnboardingResult Resumed(OnboardingSession session) =>
        new(OnboardingOutcome.Resumed, session, null, null, null, null, false, null);

    public static OnboardingResult Conversation(
        OnboardingSession session, string? question, bool isComplete) =>
        new(OnboardingOutcome.Conversation, session, null, null, null, question, isComplete, null);

    public static OnboardingResult Generated(OnboardingSession session, AiChangeSet changeSet) =>
        new(OnboardingOutcome.Generated, session, changeSet, null, null, null, false, null);

    public static OnboardingResult Updated(OnboardingSession session) =>
        new(OnboardingOutcome.Updated, session, null, null, null, null, false, null);

    public static OnboardingResult Published(
        OnboardingSession session, Site site, SitePublication publication) =>
        new(OnboardingOutcome.Published, session, null, site, publication, null, false, null);

    public static OnboardingResult Invalid(string reason) =>
        new(OnboardingOutcome.Invalid, null, null, null, null, null, false, reason);

    public static OnboardingResult NotFound(string reason) =>
        new(OnboardingOutcome.NotFound, null, null, null, null, null, false, reason);

    public static OnboardingResult Conflict(string reason) =>
        new(OnboardingOutcome.Conflict, null, null, null, null, null, false, reason);
}

/// <summary>Desfecho da operação.</summary>
public enum OnboardingOutcome
{
    Started = 1,
    Resumed = 2,
    Conversation = 3,
    Generated = 4,
    Updated = 5,
    Published = 6,
    Invalid = 7,
    NotFound = 8,
    Conflict = 9
}
