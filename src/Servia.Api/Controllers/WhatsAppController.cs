using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Servia.Api.Authorization;
using Servia.Api.Contracts;
using Servia.Domain.Common;
using Servia.Domain.Identity;
using Servia.Domain.Messaging;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;

namespace Servia.Api.Controllers;

/// <summary>
/// Endpoints de WhatsApp: conversas, templates e notificações (F13.8).
///
/// O envio real passa pelo provedor; aqui o tenant consulta e gerencia o
/// estado das conversas e dos templates.
/// </summary>
[ApiController]
[Route("api/v1/whatsapp")]
[Authorize]
public sealed class WhatsAppController(
    AppDbContext context,
    ITenantContext tenantContext,
    TimeProvider timeProvider) : ControllerBase
{
    /// <summary>Lista as conversas, com paginação.</summary>
    [HttpGet("conversations")]
    [RequirePermission(Permission.ViewCustomers)]
    [ProducesResponseType<PagedResponse<ConversationResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListConversationsAsync(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromQuery] ConversationStatus? status,
        CancellationToken cancellationToken)
    {
        var request = PageRequest.Normalize(page, pageSize);
        var query = context.Conversations.AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(conversation => conversation.Status == status.Value);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(conversation => conversation.StartedAt)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(conversation => new ConversationResponse(
                conversation.Id,
                conversation.PhoneNumber,
                conversation.CustomerId,
                conversation.Status.ToString(),
                conversation.Messages.Count))
            .ToListAsync(cancellationToken);

        return Ok(PagedResponse<ConversationResponse>.Create(items, request, total));
    }

    /// <summary>Consulta o histórico de uma conversa.</summary>
    [HttpGet("conversations/{id:guid}")]
    [RequirePermission(Permission.ViewCustomers)]
    [ProducesResponseType<ConversationDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConversationAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var conversation = await context.Conversations
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw NotFoundException.For("Conversa", id);

        var messages = conversation.Messages
            .OrderBy(message => message.SentAt)
            .Select(message => new MessageResponse(
                message.Id, message.Direction.ToString(), message.Body, message.SentAt))
            .ToList();

        return Ok(new ConversationDetailResponse(
            conversation.Id,
            conversation.PhoneNumber,
            conversation.CustomerId,
            conversation.Status.ToString(),
            conversation.AssignedTo,
            messages));
    }

    /// <summary>Escala a conversa para um membro da equipe.</summary>
    [HttpPost("conversations/{id:guid}/escalate")]
    [RequirePermission(Permission.ManageCustomers)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EscalateAsync(
        Guid id,
        [FromBody] EscalateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var conversation = await context.Conversations
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw NotFoundException.For("Conversa", id);

        conversation.EscalateToHuman(request.UserId);
        await context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>Lista os templates de mensagem do estabelecimento.</summary>
    [HttpGet("templates")]
    [RequirePermission(Permission.ViewTenantSettings)]
    [ProducesResponseType<IReadOnlyList<TemplateResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListTemplatesAsync(CancellationToken cancellationToken)
    {
        var templates = await context.MessageTemplates
            .OrderBy(template => template.Key)
            .Select(template => new TemplateResponse(
                template.Id,
                template.Key,
                template.MessageType.ToString(),
                template.Body,
                template.IsActive))
            .ToListAsync(cancellationToken);

        return Ok(templates);
    }

    /// <summary>Cria um template de mensagem.</summary>
    [HttpPost("templates")]
    [RequirePermission(Permission.ManageTenantSettings)]
    [ProducesResponseType<TemplateResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateTemplateAsync(
        [FromBody] CreateTemplateRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var template = MessageTemplate.Create(
            RequireTenant(), request.Key, MessageChannel.WhatsApp, request.MessageType, request.Body);

        context.MessageTemplates.Add(template);
        await context.SaveChangesAsync(cancellationToken);

        return Created(
            $"/api/v1/whatsapp/templates/{template.Id}",
            new TemplateResponse(template.Id, template.Key,
                template.MessageType.ToString(), template.Body, template.IsActive));
    }

    /// <summary>Lista as notificações pendentes de envio.</summary>
    [HttpGet("notifications")]
    [RequirePermission(Permission.ViewCustomers)]
    [ProducesResponseType<PagedResponse<NotificationResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListNotificationsAsync(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        CancellationToken cancellationToken)
    {
        var request = PageRequest.Normalize(page, pageSize);
        var query = context.Notifications
            .Where(notification => notification.Status == NotificationStatus.Scheduled);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(notification => notification.ScheduledAt)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(notification => new NotificationResponse(
                notification.Id,
                notification.MessageType.ToString(),
                notification.Recipient,
                notification.ScheduledAt,
                notification.Status.ToString()))
            .ToListAsync(cancellationToken);

        return Ok(PagedResponse<NotificationResponse>.Create(items, request, total));
    }

    /// <summary>Consulta as permissões de envio do estabelecimento.</summary>
    [HttpGet("policy")]
    [RequirePermission(Permission.ViewTenantSettings)]
    [ProducesResponseType<PolicyResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPolicyAsync(CancellationToken cancellationToken)
    {
        var policy = await context.MessagingPolicies
            .FirstOrDefaultAsync(cancellationToken);

        var allowed = policy?.AllowedTypes.Select(type => type.ToString()).ToList()
            ?? [];

        return Ok(new PolicyResponse(allowed));
    }

    private Guid RequireTenant() =>
        tenantContext.TenantId ?? throw new DomainRuleException("Estabelecimento não identificado.");
}

/// <summary>Corpo de escalonamento.</summary>
public sealed record EscalateRequest(Guid UserId);

/// <summary>Corpo de criação de template.</summary>
public sealed record CreateTemplateRequest(
    [Required, MaxLength(100)] string Key,
    MessageType MessageType,
    [Required, MaxLength(2000)] string Body);

/// <summary>Representação de conversa.</summary>
public sealed record ConversationResponse(
    Guid Id, string PhoneNumber, Guid? CustomerId, string Status, int MessageCount);

/// <summary>Representação detalhada de conversa.</summary>
public sealed record ConversationDetailResponse(
    Guid Id, string PhoneNumber, Guid? CustomerId, string Status,
    Guid? AssignedTo, IReadOnlyList<MessageResponse> Messages);

/// <summary>Representação de mensagem.</summary>
public sealed record MessageResponse(
    Guid Id, string Direction, string Body, DateTimeOffset SentAt);

/// <summary>Representação de template.</summary>
public sealed record TemplateResponse(
    Guid Id, string Key, string MessageType, string Body, bool IsActive);

/// <summary>Representação de notificação.</summary>
public sealed record NotificationResponse(
    Guid Id, string MessageType, string Recipient,
    DateTimeOffset? ScheduledAt, string Status);

/// <summary>Representação de política de envio.</summary>
public sealed record PolicyResponse(IReadOnlyList<string> AllowedTypes);
