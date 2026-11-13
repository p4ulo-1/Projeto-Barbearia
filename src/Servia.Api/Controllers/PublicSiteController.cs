using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Servia.Api.Contracts;
using Servia.Api.Middleware;
using Servia.Application.Appointments;
using Servia.Application.Sites;
using Servia.Domain.Common;
using Servia.Domain.Customers;
using Servia.Domain.Sites;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Integrations;
using Servia.Infrastructure.Persistence;

namespace Servia.Api.Controllers;

/// <summary>
/// Endpoints públicos dos sites dos estabelecimentos (item 3 e 3.1).
///
/// <b>Resolução por host:</b> o estabelecimento vem do subdomínio ou domínio
/// próprio da requisição (header <c>Host</c> ou parâmetro <c>host</c>). O
/// cliente não informa o tenant — ele é resolvido do endereço.
///
/// <b>Somente publicado:</b> responde 404 quando o site não está publicado.
/// Rascunho e proposta nunca são servidos.
///
/// <b>Segurança:</b> o tenant resolvido pelo host alimenta o contexto apenas
/// para consultar e agendar. Isso não substitui a autorização das operações
/// administrativas — aquelas usam a claim do token.
///
/// <b>Isolamento:</b> depois de resolvido o tenant, todo acesso passa pelo
/// filtro global. Um site nunca enxerga serviço, profissional ou agendamento
/// de outro estabelecimento.
/// </summary>
[ApiController]
[Route("api/v1/public/site")]
[AllowAnonymous]
public sealed class PublicSiteController(
    IPublicSiteReader reader,
    AppDbContext context,
    AppointmentService appointmentService,
    AvailabilityService availabilityService,
    ITenantContext tenantContext,
    IPublicBookingThrottle bookingThrottle,
    PublicBookingThrottleOptions throttleOptions,
    TimeProvider timeProvider) : ControllerBase
{
    /// <summary>
    /// Site publicado do estabelecimento identificado pelo host.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<PublicSiteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(
        [FromQuery] string? host,
        CancellationToken cancellationToken)
    {
        var site = await ResolveAsync(host, cancellationToken);
        tenantContext.SetTenant(site.TenantId);

        return Ok(new PublicSiteResponse(
            site.Subdomain,
            site.BusinessName,
            site.Version,
            $"primary={site.Theme.PrimaryColor}; secondary={site.Theme.SecondaryColor}; "
                + $"font={site.Theme.FontFamily}; layout={site.Theme.LayoutStyle}",
            site.Pages
                .Select(page => new PublicPageResponse(
                    page.Slug,
                    page.Title,
                    page.Order,
                    page.Sections
                        .Select(section => new PublicSectionResponse(
                            section.ComponentType.ToString(),
                            section.Order,
                            section.Configuration))
                        .ToList()))
                .ToList()));
    }

    /// <summary>
    /// Serviços ativos do estabelecimento, para o visitante escolher.
    /// Identificadores internos não são expostos: o visitante usa o serviço
    /// pelo identificador público que esta resposta devolve apenas para
    /// concluir o agendamento.
    /// </summary>
    [HttpGet("services")]
    [ProducesResponseType<IReadOnlyList<PublicServiceResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetServicesAsync(
        [FromQuery] string? host,
        CancellationToken cancellationToken)
    {
        var site = await ResolveAsync(host, cancellationToken);
        tenantContext.SetTenant(site.TenantId);

        var services = await context.Services
            .Where(service => service.IsActive)
            .OrderBy(service => service.Name)
            .Select(service => new PublicServiceResponse(
                service.Id,
                service.Name,
                service.Description,
                service.Price.Amount,
                service.Price.Currency,
                service.DurationMinutes))
            .ToListAsync(cancellationToken);

        return Ok(services);
    }

    /// <summary>
    /// Profissionais ativos, com os serviços que cada um executa.
    /// </summary>
    [HttpGet("professionals")]
    [ProducesResponseType<IReadOnlyList<PublicProfessionalResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfessionalsAsync(
        [FromQuery] string? host,
        CancellationToken cancellationToken)
    {
        var site = await ResolveAsync(host, cancellationToken);
        tenantContext.SetTenant(site.TenantId);

        var professionals = await context.Professionals
            .Where(professional => professional.IsActive)
            .OrderBy(professional => professional.Name)
            .ToListAsync(cancellationToken);

        var links = await context.ProfessionalServices
            .ToListAsync(cancellationToken);

        var result = professionals
            .Select(professional => new PublicProfessionalResponse(
                professional.Id,
                professional.Name,
                links
                    .Where(link => link.ProfessionalId == professional.Id)
                    .Select(link => link.ServiceId)
                    .ToList()))
            .ToList();

        return Ok(result);
    }

    /// <summary>
    /// Horários disponíveis de um profissional numa data (item 3.1).
    /// Reaproveita a regra de disponibilidade da agenda.
    /// </summary>
    [HttpGet("availability")]
    [ProducesResponseType<PublicAvailabilityResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAvailabilityAsync(
        [FromQuery] string? host,
        [FromQuery] Guid professionalId,
        [FromQuery] Guid serviceId,
        [FromQuery] DateOnly date,
        CancellationToken cancellationToken)
    {
        var site = await ResolveAsync(host, cancellationToken);
        tenantContext.SetTenant(site.TenantId);

        var service = await context.Services
            .FirstOrDefaultAsync(item => item.Id == serviceId && item.IsActive, cancellationToken)
            ?? throw NotFoundException.For("Serviço", serviceId);

        // C9: mesma regra do painel, via AvailabilityService.
        var slots = await availabilityService.GetAvailableSlotsAsync(
            site.TenantId, professionalId, date, service.DurationMinutes, cancellationToken);

        return Ok(new PublicAvailabilityResponse(professionalId, date, slots));
    }

    /// <summary>
    /// Agenda um atendimento pelo site público (item 3.1).
    ///
    /// O visitante informa nome e telefone; o cliente é localizado por
    /// telefone no estabelecimento ou criado. A criação do agendamento
    /// reaproveita <see cref="AppointmentService"/>, respeitando a regra de
    /// não sobreposição e o isolamento.
    /// </summary>
    [HttpPost("appointments")]
    [ProducesResponseType<PublicAppointmentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> BookAsync(
        [FromBody] PublicBookingRequest request,
        [FromQuery] string? host,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var site = await ResolveAsync(host, cancellationToken);
        tenantContext.SetTenant(site.TenantId);

        // C3: limita tentativas de agendamento por (estabelecimento, origem).
        // A origem respeita proxies confiaveis; X-Forwarded-For nao e aceito
        // de qualquer cliente.
        var clientKey = ClientOriginResolver.Resolve(HttpContext, throttleOptions);

        if (!await bookingThrottle.TryAcquireAsync(site.TenantId, clientKey, cancellationToken))
        {
            return StatusCode(
                StatusCodes.Status429TooManyRequests,
                new ErrorResponse("Muitas tentativas de agendamento. Tente novamente mais tarde."));
        }

        var service = await context.Services
            .FirstOrDefaultAsync(item => item.Id == request.ServiceId && item.IsActive, cancellationToken)
            ?? throw NotFoundException.For("Serviço", request.ServiceId);

        var professional = await context.Professionals
            .FirstOrDefaultAsync(item => item.Id == request.ProfessionalId && item.IsActive, cancellationToken)
            ?? throw NotFoundException.For("Profissional", request.ProfessionalId);

        // C9: a validacao do agendamento usa a MESMA regra da consulta de
        // disponibilidade (AvailabilityService), evitando divergencia.
        var slot = await availabilityService.CheckSlotAsync(
            site.TenantId, professional.Id, request.Date, request.Time,
            service.DurationMinutes, cancellationToken);

        if (!slot.IsAvailable)
        {
            // Conflito de horario e 409; horario invalido e 400.
            return slot.IsConflict
                ? Conflict(new ErrorResponse(slot.Reason ?? "Horário indisponível."))
                : BadRequest(new ErrorResponse(slot.Reason ?? "Horário indisponível."));
        }

        // Cliente localizado por telefone para nao duplicar cadastro.
        var phone = request.Phone.Trim();
        var existingCustomer = await context.Customers
            .FirstOrDefaultAsync(item => item.Phone == phone && item.IsActive, cancellationToken);

        // C6: o cliente novo NAO e persistido aqui. Ele entra na mesma
        // transacao do agendamento (newCustomer); se o horario estiver ocupado,
        // nada e gravado e nao sobra cliente orfao.
        var newCustomer = existingCustomer is null
            ? Customer.Create(site.TenantId, request.Name, phone, request.Email, now: timeProvider.GetUtcNow())
            : null;

        var customerId = existingCustomer?.Id ?? newCustomer!.Id;

        var result = await appointmentService.CreateAsync(
            new CreateAppointmentCommand(
                site.TenantId,
                customerId,
                service.Id,
                professional.Id,
                request.Date,
                request.Time,
                service.DurationMinutes),
            newCustomer,
            cancellationToken);

        if (result.IsSuccess)
        {
            var appointment = result.Appointment!;

            return Created(
                $"/api/v1/public/site/appointments/{appointment.Id}",
                new PublicAppointmentResponse(
                    appointment.Id, request.Date, request.Time, service.DurationMinutes));
        }

        var failure = result.Failure!;

        return failure.Kind == AppointmentFailureKind.Conflict
            ? Conflict(new ErrorResponse(failure.Message))
            : BadRequest(new ErrorResponse(failure.Message));
    }

    /// <summary>
    /// Resolve o site publicado pelo host e fixa o tenant no contexto.
    ///
    /// O host vem do header <c>Host</c> quando não informado na query: é o
    /// endereço pelo qual o visitante chegou, não um identificador livre.
    /// </summary>
    private async Task<PublicSite> ResolveAsync(string? host, CancellationToken cancellationToken)
    {
        var resolvedHost = string.IsNullOrWhiteSpace(host) ? Request.Host.Value : host;

        var site = await reader.FindPublishedByHostAsync(resolvedHost ?? string.Empty, cancellationToken)
            ?? throw NotFoundException.For("Site", resolvedHost ?? string.Empty);

        // O tenant e fixado pelo chamador (SetTenant), fora do metodo async.
        // AsyncLocal nao propaga alteracao feita dentro de um metodo async para
        // o chamador; por isso a fixacao ocorre na acao, nao aqui.
        return site;
    }
}

// ---------- Contratos públicos ----------

/// <summary>Representação do site publicado.</summary>
public sealed record PublicSiteResponse(
    string Subdomain,
    string BusinessName,
    int Version,
    string Theme,
    IReadOnlyList<PublicPageResponse> Pages);

/// <summary>Página pública.</summary>
public sealed record PublicPageResponse(
    string Slug,
    string Title,
    int Order,
    IReadOnlyList<PublicSectionResponse> Sections);

/// <summary>Seção pública.</summary>
public sealed record PublicSectionResponse(
    string ComponentType,
    int Order,
    IReadOnlyDictionary<string, string> Configuration);

/// <summary>Serviço público.</summary>
public sealed record PublicServiceResponse(
    Guid Id, string Name, string? Description, decimal Price, string Currency, int DurationMinutes);

/// <summary>Profissional público.</summary>
public sealed record PublicProfessionalResponse(
    Guid Id, string Name, IReadOnlyList<Guid> ServiceIds);

/// <summary>Disponibilidade pública.</summary>
public sealed record PublicAvailabilityResponse(
    Guid ProfessionalId, DateOnly Date, IReadOnlyList<TimeOnly> Slots);

/// <summary>Corpo de agendamento público.</summary>
public sealed record PublicBookingRequest(
    [Required, MaxLength(150)] string Name,
    [Required, MaxLength(30)] string Phone,
    [EmailAddress, MaxLength(200)] string? Email,
    Guid ServiceId,
    Guid ProfessionalId,
    DateOnly Date,
    TimeOnly Time);

/// <summary>Agendamento público confirmado.</summary>
public sealed record PublicAppointmentResponse(
    Guid Id, DateOnly Date, TimeOnly Time, int DurationMinutes);
