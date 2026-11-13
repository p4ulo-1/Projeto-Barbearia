using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Servia.Api.Authorization;
using Servia.Api.Contracts;
using Servia.Application.Onboarding;
using Servia.Application.Sites;
using Servia.Domain.Ai;
using Servia.Domain.Common;
using Servia.Domain.Identity;
using Servia.Domain.Sites;
using Servia.Domain.Tenancy;

namespace Servia.Api.Controllers;

/// <summary>
/// Endpoints do onboarding assistido por IA e da publicação do site (itens 1 e 2).
///
/// <b>Fluxo:</b> iniciar → conversar → gerar → ajustar → aprovar → publicar.
/// O proprietário controla cada etapa; a IA nunca publica sozinha.
///
/// <b>Segurança:</b> o tenant vem da claim do token, nunca do corpo. O
/// proprietário só acessa o próprio estabelecimento — o repositório e o
/// filtro global garantem isso.
/// </summary>
[ApiController]
[Route("api/v1/onboarding")]
[Authorize]
public sealed class OnboardingController(
    OnboardingService onboarding,
    IOnboardingRepository repository,
    ITenantContext tenantContext) : ControllerBase
{
    private const int MaxResumeListSize = 20;

    /// <summary>Inicia (ou retoma) o onboarding (item 4).</summary>
    [HttpPost("start")]
    [RequirePermission(Permission.ManageTenantSettings)]
    [ProducesResponseType<OnboardingSessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> StartAsync(
        [FromBody] StartOnboardingRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tenantId = RequireTenant();

        var result = await onboarding.StartAsync(
            tenantId, request.BusinessName, request.Segment, cancellationToken);

        return Respond(result);
    }

    /// <summary>Lista as sessões de onboarding do estabelecimento.</summary>
    [HttpGet("sessions")]
    [RequirePermission(Permission.ViewTenantSettings)]
    [ProducesResponseType<IReadOnlyList<OnboardingSessionResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync(CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();
        var sessions = await onboarding.ListAsync(tenantId, cancellationToken);

        return Ok(sessions
            .Take(MaxResumeListSize)
            .Select(OnboardingSessionResponse.From)
            .ToList());
    }

    /// <summary>Retoma uma sessão (item 1.2).</summary>
    [HttpGet("sessions/{sessionId:guid}")]
    [RequirePermission(Permission.ViewTenantSettings)]
    [ProducesResponseType<OnboardingSessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResumeAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();
        var result = await onboarding.ResumeAsync(tenantId, sessionId, cancellationToken);

        return Respond(result);
    }

    /// <summary>Recupera o histórico da conversa (item 4).</summary>
    [HttpGet("sessions/{sessionId:guid}/messages")]
    [RequirePermission(Permission.ViewTenantSettings)]
    [ProducesResponseType<IReadOnlyList<ConversationMessageResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMessagesAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();
        var session = await repository.FindSessionAsync(tenantId, sessionId, cancellationToken);

        if (session is null)
        {
            throw NotFoundException.For("Sessão de onboarding", sessionId);
        }

        var messages = session.Messages
            .OrderBy(message => message.SentAt)
            .Select(message => new ConversationMessageResponse(
                message.Role.ToString(), message.Content, message.SentAt))
            .ToList();

        return Ok(messages);
    }

    /// <summary>Envia uma mensagem do proprietário e recebe a próxima pergunta (item 1.2).</summary>
    [HttpPost("sessions/{sessionId:guid}/messages")]
    [RequirePermission(Permission.ManageTenantSettings)]
    [ProducesResponseType<ConversationTurnResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendMessageAsync(
        Guid sessionId,
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tenantId = RequireTenant();

        var result = await onboarding.SendMessageAsync(
            tenantId, sessionId, request.Message, cancellationToken);

        return RespondConversation(result);
    }

    /// <summary>Gera a configuração do site a partir da conversa (item 1.4).</summary>
    [HttpPost("sessions/{sessionId:guid}/generate")]
    [RequirePermission(Permission.ManageTenantSettings)]
    [ProducesResponseType<ConfigurationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();
        var result = await onboarding.GenerateConfigurationAsync(tenantId, sessionId, cancellationToken);

        return RespondConfiguration(result);
    }

    /// <summary>Solicita ajustes à IA, preservando o histórico (item 2.1).</summary>
    [HttpPost("sessions/{sessionId:guid}/adjustments")]
    [RequirePermission(Permission.ManageTenantSettings)]
    [ProducesResponseType<ConfigurationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AdjustAsync(
        Guid sessionId,
        [FromBody] RequestAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tenantId = RequireTenant();

        var result = await onboarding.RequestAdjustmentsAsync(
            tenantId, sessionId, request.Instruction, cancellationToken);

        return RespondConfiguration(result);
    }

    /// <summary>Envia a configuração para aprovação (item 2.2).</summary>
    [HttpPost("sessions/{sessionId:guid}/submit")]
    [RequirePermission(Permission.ManageTenantSettings)]
    [ProducesResponseType<OnboardingSessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();
        var result = await onboarding.SubmitForApprovalAsync(tenantId, sessionId, cancellationToken);

        return Respond(result);
    }

    /// <summary>Aprova a configuração explicitamente (item 2.2).</summary>
    [HttpPost("sessions/{sessionId:guid}/approve")]
    [RequirePermission(Permission.ManageTenantSettings)]
    [ProducesResponseType<OnboardingSessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ApproveAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();
        var result = await onboarding.ApproveAsync(
            tenantId, sessionId, RequireUserId(), cancellationToken);

        return Respond(result);
    }

    /// <summary>Publica o site a partir da configuração aprovada (item 2.3).</summary>
    [HttpPost("sessions/{sessionId:guid}/publish")]
    [RequirePermission(Permission.ManageTenantSettings)]
    [ProducesResponseType<PublishedSiteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PublishAsync(
        Guid sessionId,
        [FromBody] PublishSiteRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tenantId = RequireTenant();

        var result = await onboarding.PublishAsync(
            tenantId, sessionId, RequireUserId(), request.Subdomain, cancellationToken);

        if (result.Outcome is OnboardingOutcome.Published
            && result.Site is not null && result.Publication is not null)
        {
            return Ok(new PublishedSiteResponse(
                result.Site.Subdomain,
                result.Publication.Version,
                result.Publication.PublishedAt));
        }

        return Respond(result);
    }

    /// <summary>
    /// Recupera a prévia da configuração atual (item 2.1).
    ///
    /// A prévia usa o mesmo snapshot que o site público, para que o que o
    /// proprietário vê seja o que será publicado.
    /// </summary>
    [HttpGet("sessions/{sessionId:guid}/preview")]
    [RequirePermission(Permission.ViewTenantSettings)]
    [ProducesResponseType<ConfigurationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PreviewAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();
        var session = await repository.FindSessionAsync(tenantId, sessionId, cancellationToken)
            ?? throw NotFoundException.For("Sessão de onboarding", sessionId);

        if (session.GeneratedChangeSetId is null)
        {
            return Ok(new ConfigurationResponse(
                session.Status.ToString(), null, null,
                Array.Empty<ConfigurationSectionResponse>(),
                "Nenhuma configuração gerada ainda."));
        }

        var changeSet = await repository.FindChangeSetAsync(
            tenantId, session.GeneratedChangeSetId.Value, cancellationToken);

        if (changeSet is null)
        {
            return Ok(new ConfigurationResponse(
                session.Status.ToString(), null, null,
                Array.Empty<ConfigurationSectionResponse>(),
                "A configuração não foi encontrada."));
        }

        return Ok(ConfigurationResponse.From(session, changeSet));
    }

    private IActionResult Respond(OnboardingResult result) => result.Outcome switch
    {
        OnboardingOutcome.NotFound =>
            NotFound(new ErrorResponse(result.FailureReason ?? "Não encontrado.")),
        OnboardingOutcome.Invalid =>
            BadRequest(new ErrorResponse(result.FailureReason ?? "Operação inválida.")),
        OnboardingOutcome.Conflict =>
            Conflict(new ErrorResponse(result.FailureReason ?? "Conflito de concorrência.")),
        OnboardingOutcome.Published when result.Site is not null && result.Publication is not null =>
            Ok(new PublishedSiteResponse(
                result.Site.Subdomain,
                result.Publication.Version,
                result.Publication.PublishedAt)),
        _ when result.Session is not null =>
            Ok(OnboardingSessionResponse.From(result.Session)),
        _ => StatusCode(StatusCodes.Status500InternalServerError,
            new ErrorResponse("Estado inesperado do onboarding."))
    };

    private IActionResult RespondConversation(OnboardingResult result) =>
        result.Outcome is OnboardingOutcome.NotFound
            ? NotFound(new ErrorResponse(result.FailureReason ?? "Não encontrado."))
            : result.Outcome is OnboardingOutcome.Invalid
                ? BadRequest(new ErrorResponse(result.FailureReason ?? "Operação inválida."))
                : Ok(new ConversationTurnResponse(
                    result.Question,
                    result.IsConversationComplete,
                    result.Session!.Status.ToString()));

    private IActionResult RespondConfiguration(OnboardingResult result)
    {
        if (result.Outcome is OnboardingOutcome.NotFound)
        {
            return NotFound(new ErrorResponse(result.FailureReason ?? "Não encontrado."));
        }

        if (result.Outcome is OnboardingOutcome.Invalid || result.ChangeSet is null)
        {
            return BadRequest(new ErrorResponse(result.FailureReason ?? "Operação inválida."));
        }

        return Ok(ConfigurationResponse.From(result.Session!, result.ChangeSet));
    }

    private Guid RequireTenant() =>
        tenantContext.TenantId
            ?? throw new DomainRuleException("Estabelecimento não identificado.");

    private Guid RequireUserId()
    {
        var claim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(claim, out var userId)
            ? userId
            : throw new DomainRuleException("Usuário não identificado.");
    }
}

// ---------- Contratos de entrada ----------

/// <summary>Corpo de início do onboarding.</summary>
public sealed record StartOnboardingRequest(
    [Required, MaxLength(200)] string BusinessName,
    SegmentType Segment);

/// <summary>Corpo de envio de mensagem.</summary>
public sealed record SendMessageRequest(
    [Required, MaxLength(4000)] string Message);

/// <summary>Corpo de solicitação de ajuste.</summary>
public sealed record RequestAdjustmentRequest(
    [Required, MaxLength(2000)] string Instruction);

/// <summary>Corpo de publicação.</summary>
public sealed record PublishSiteRequest(
    [Required, MaxLength(40)] string Subdomain);

// ---------- Contratos de saída ----------

/// <summary>Representação da sessão de onboarding.</summary>
public sealed record OnboardingSessionResponse(
    Guid Id,
    string BusinessName,
    string Segment,
    string Status,
    int MessageCount,
    Guid? GeneratedChangeSetId,
    DateTimeOffset StartedAt)
{
    public static OnboardingSessionResponse From(OnboardingSession session) =>
        new(session.Id, session.BusinessName, session.Segment.ToString(), session.Status.ToString(),
            session.Messages.Count, session.GeneratedChangeSetId, session.StartedAt);
}

/// <summary>Turno da conversa.</summary>
public sealed record ConversationTurnResponse(
    string? Question,
    bool IsComplete,
    string Status);

/// <summary>Mensagem do histórico.</summary>
public sealed record ConversationMessageResponse(
    string Role,
    string Content,
    DateTimeOffset SentAt);

/// <summary>Configuração gerada, usada na prévia e na revisão.</summary>
public sealed record ConfigurationResponse(
    string Status,
    string? Summary,
    string? Theme,
    IReadOnlyList<ConfigurationSectionResponse> Sections,
    string? Note)
{
    public static ConfigurationResponse From(OnboardingSession session, AiChangeSet changeSet) =>
        new(session.Status.ToString(),
            changeSet.Summary,
            $"primary={changeSet.Theme.PrimaryColor}; secondary={changeSet.Theme.SecondaryColor}; "
                + $"font={changeSet.Theme.FontFamily}; layout={changeSet.Theme.LayoutStyle}",
            changeSet.Sections
                .OrderBy(section => section.SortOrder)
                .Select(section => new ConfigurationSectionResponse(
                    section.ComponentType.ToString(),
                    section.SortOrder,
                    section.Configuration))
                .ToList(),
            null);
}

/// <summary>Seção da configuração.</summary>
public sealed record ConfigurationSectionResponse(
    string ComponentType,
    int SortOrder,
    IReadOnlyDictionary<string, string> Configuration);

/// <summary>Site publicado.</summary>
public sealed record PublishedSiteResponse(
    string Subdomain,
    int Version,
    DateTimeOffset PublishedAt);
