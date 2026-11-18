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
using Servia.Domain.Team;
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
    /// Profissionais ativos que podem executar ao menos um serviço público.
    ///
    /// <b>Serviços retornados são válidos e ativos.</b> Um vínculo com serviço
    /// inativo não autoriza agendamento, então não é apresentado: mostrá-lo
    /// faria o visitante escolher um profissional que o backend recusaria.
    ///
    /// <b>Profissional sem serviço público não aparece.</b> Não há nada que
    /// ele possa executar, e a seleção do profissional depende do serviço.
    ///
    /// <b>Elegibilidade não é disponibilidade.</b> Este endpoint responde
    /// "quem executa o serviço"; "quem tem horário livre" é respondido pela
    /// consulta de disponibilidade.
    ///
    /// <paramref name="serviceId"/> é opcional e apenas filtra: o contrato
    /// continua devolvendo <c>ServiceIds</c>, então o consumidor pode filtrar
    /// localmente sem depender do parâmetro.
    /// </summary>
    [HttpGet("professionals")]
    [ProducesResponseType<IReadOnlyList<PublicProfessionalResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfessionalsAsync(
        [FromQuery] string? host,
        [FromQuery] Guid? serviceId,
        CancellationToken cancellationToken)
    {
        var site = await ResolveAsync(host, cancellationToken);
        tenantContext.SetTenant(site.TenantId);

        // Apenas vínculos com serviços ativos do tenant.
        var activeLinks = await context.ProfessionalServices
            .Where(link => context.Services.Any(service =>
                service.Id == link.ServiceId && service.IsActive))
            .Select(link => new { link.ProfessionalId, link.ServiceId })
            .ToListAsync(cancellationToken);

        var professionals = await context.Professionals
            .Where(professional => professional.IsActive)
            .OrderBy(professional => professional.Name)
            .ToListAsync(cancellationToken);

        var result = professionals
            .Where(BookingEligibility.IsOperationallyEligible)
            .Select(professional => new PublicProfessionalResponse(
                professional.Id,
                professional.Name,
                activeLinks
                    .Where(link => link.ProfessionalId == professional.Id)
                    .Select(link => link.ServiceId)
                    .ToList()))
            // Sem serviço público, não há o que agendar.
            .Where(professional => professional.ServiceIds.Count > 0)
            // Filtro opcional pelo serviço escolhido.
            .Where(professional => !serviceId.HasValue
                || professional.ServiceIds.Contains(serviceId.Value))
            .ToList();

        return Ok(result);
    }

    /// <summary>
    /// Horários disponíveis de um profissional numa data (item 3.1).
    /// Reaproveita a regra de disponibilidade da agenda.
    ///
    /// <b>Três respostas distintas, que o consumidor não pode confundir:</b>
    /// <list type="bullet">
    /// <item><b>400</b> — pedido incompleto ou inválido (sem serviço, sem
    /// profissional, sem data).</item>
    /// <item><b>404</b> — serviço ou profissional inexistente ou inativo no
    /// estabelecimento.</item>
    /// <item><b>200 com lista vazia</b> — pedido válido, agenda realmente sem
    /// horário livre.</item>
    /// </list>
    /// Usar 200 vazio para esconder erro de validação faria o visitante
    /// concluir que a agenda está cheia quando o pedido está errado.
    /// </summary>
    [HttpGet("availability")]
    [ProducesResponseType<PublicAvailabilityResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAvailabilityAsync(
        [FromQuery] string? host,
        [FromQuery] Guid professionalId,
        [FromQuery] Guid serviceId,
        [FromQuery] DateOnly date,
        CancellationToken cancellationToken)
    {
        if (serviceId == Guid.Empty)
        {
            return BadRequest(new ErrorResponse("O serviço é obrigatório."));
        }

        if (professionalId == Guid.Empty)
        {
            return BadRequest(new ErrorResponse("O profissional é obrigatório."));
        }

        if (date == default)
        {
            return BadRequest(new ErrorResponse("A data é obrigatória."));
        }

        var site = await ResolveAsync(host, cancellationToken);
        tenantContext.SetTenant(site.TenantId);

        var service = await context.Services
            .FirstOrDefaultAsync(item => item.Id == serviceId && item.IsActive, cancellationToken)
            ?? throw NotFoundException.For("Serviço", serviceId);

        var professional = await context.Professionals
            .FirstOrDefaultAsync(
                item => item.Id == professionalId && item.IsActive, cancellationToken)
            ?? throw NotFoundException.For("Profissional", professionalId);

        // C9: mesma regra do painel, via AvailabilityService. O serviço entra
        // na avaliação: um profissional que não o executa não tem horário.
        var slots = await availabilityService.GetAvailableSlotsAsync(
            site.TenantId, professional.Id, service.Id, date, cancellationToken);

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
        // disponibilidade (AvailabilityService), incluindo o vinculo
        // profissional-servico. A duracao vem do servico, nao do pedido.
        var slot = await availabilityService.CheckSlotAsync(
            site.TenantId, professional.Id, service.Id, request.Date, request.Time,
            cancellationToken);

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
                request.Time),
            newCustomer,
            cancellationToken);

        if (result.IsSuccess)
        {
            var appointment = result.Appointment!;

            return Created(
                $"/api/v1/public/site/appointments/{appointment.Id}",
                new PublicAppointmentResponse(
                    appointment.Id, request.Date, request.Time, appointment.DurationMinutes));
        }

        var failure = result.Failure!;

        return failure.Kind switch
        {
            AppointmentFailureKind.Conflict => Conflict(new ErrorResponse(failure.Message)),
            AppointmentFailureKind.NotFound => NotFound(new ErrorResponse(failure.Message)),
            _ => BadRequest(new ErrorResponse(failure.Message))
        };
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
