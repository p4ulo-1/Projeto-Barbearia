using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Servia.Api.Authorization;
using Servia.Api.Contracts;
using Servia.Domain.Common;
using Servia.Domain.Finance;
using Servia.Domain.Identity;
using Servia.Domain.Tenancy;
using Servia.Infrastructure.Persistence;

namespace Servia.Api.Controllers;

/// <summary>
/// Endpoints financeiros do estabelecimento (F13.7).
///
/// Registra pagamentos, lançamentos e comissões, e devolve relatórios do
/// período. O financeiro do tenant é independente do billing da plataforma
/// (ADR-020).
/// </summary>
[ApiController]
[Route("api/v1/finance")]
[Authorize]
public sealed class FinanceController(
    AppDbContext context,
    ITenantContext tenantContext,
    TimeProvider timeProvider) : ControllerBase
{
    /// <summary>Registra um pagamento presencial.</summary>
    [HttpPost("payments")]
    [RequirePermission(Permission.ManageFinancialEntries)]
    [ProducesResponseType<PaymentResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> RegisterPaymentAsync(
        [FromBody] RegisterPaymentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payment = Payment.RegisterInPerson(
            RequireTenant(),
            request.AppointmentId,
            Money.From(request.Amount),
            request.Method,
            timeProvider.GetUtcNow());

        context.Payments.Add(payment);
        await context.SaveChangesAsync(cancellationToken);

        return Created(
            $"/api/v1/finance/payments/{payment.Id}",
            new PaymentResponse(payment.Id, payment.Amount.Amount, payment.Method.ToString(),
                payment.Status.ToString()));
    }

    /// <summary>Estorna um pagamento, total ou parcialmente.</summary>
    [HttpPost("payments/{id:guid}/refund")]
    [RequirePermission(Permission.ManageFinancialEntries)]
    [ProducesResponseType<PaymentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RefundAsync(
        Guid id,
        [FromBody] RefundRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payment = await context.Payments
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw NotFoundException.For("Pagamento", id);

        payment.Refund(Money.From(request.Amount));
        await context.SaveChangesAsync(cancellationToken);

        return Ok(new PaymentResponse(payment.Id, payment.Amount.Amount,
            payment.Method.ToString(), payment.Status.ToString()));
    }

    /// <summary>Registra uma receita ou despesa.</summary>
    [HttpPost("transactions")]
    [RequirePermission(Permission.ManageFinancialEntries)]
    [ProducesResponseType<TransactionResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateTransactionAsync(
        [FromBody] CreateTransactionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var transaction = Transaction.Create(
            RequireTenant(),
            request.Type,
            Money.From(request.Amount),
            request.Description,
            timeProvider.GetUtcNow(),
            request.CategoryId);

        context.Transactions.Add(transaction);
        await context.SaveChangesAsync(cancellationToken);

        return Created(
            $"/api/v1/finance/transactions/{transaction.Id}",
            new TransactionResponse(transaction.Id, transaction.Type.ToString(),
                transaction.Amount.Amount, transaction.Description));
    }

    /// <summary>Lista os lançamentos do período, com paginação.</summary>
    [HttpGet("transactions")]
    [RequirePermission(Permission.ViewFinancialReports)]
    [ProducesResponseType<PagedResponse<TransactionResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListTransactionsAsync(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var request = PageRequest.Normalize(page, pageSize);
        var query = context.Transactions.AsQueryable();

        if (from.HasValue)
        {
            var start = new DateTimeOffset(from.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(transaction => transaction.OccurredAt >= start);
        }

        if (to.HasValue)
        {
            var end = new DateTimeOffset(to.Value.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
            query = query.Where(transaction => transaction.OccurredAt <= end);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(transaction => transaction.OccurredAt)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(transaction => new TransactionResponse(
                transaction.Id,
                transaction.Type.ToString(),
                transaction.Amount.Amount,
                transaction.Description))
            .ToListAsync(cancellationToken);

        return Ok(PagedResponse<TransactionResponse>.Create(items, request, total));
    }

    /// <summary>Gera o relatório financeiro do período.</summary>
    [HttpGet("report")]
    [RequirePermission(Permission.ViewFinancialReports)]
    [ProducesResponseType<ReportResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReportAsync(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken)
    {
        var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var end = new DateTimeOffset(to.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

        // O SQLite nao traduz comparacao de DateTimeOffset com offset zero.
        // Materializa e compara em memoria.
        var allTransactions = await context.Transactions.ToListAsync(cancellationToken);

        var transactions = allTransactions
            .Where(transaction => transaction.OccurredAt >= start && transaction.OccurredAt <= end)
            .ToList();

        var appointmentCount = await context.Appointments
            .CountAsync(appointment => appointment.Date >= from && appointment.Date <= to,
                cancellationToken);

        var report = FinancialReport.Build(from, to, transactions, appointmentCount);

        return Ok(new ReportResponse(
            report.PeriodStart,
            report.PeriodEnd,
            report.TotalIncome.Amount,
            report.TotalExpense.Amount,
            report.Balance.Amount,
            report.AverageTicket.Amount,
            report.AppointmentCount));
    }

    /// <summary>Consulta o extrato de comissões de um profissional.</summary>
    [HttpGet("commissions/{professionalId:guid}")]
    [RequirePermission(Permission.ViewFinancialReports)]
    [ProducesResponseType<CommissionStatementResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCommissionStatementAsync(
        Guid professionalId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken)
    {
        var entries = await context.CommissionEntries
            .Where(entry =>
                entry.ProfessionalId == professionalId
                && entry.ReferencePeriod >= from
                && entry.ReferencePeriod <= to)
            .ToListAsync(cancellationToken);

        var statement = CommissionStatement.Build(professionalId, from, to, entries);

        return Ok(new CommissionStatementResponse(
            professionalId,
            statement.TotalPending.Amount,
            statement.TotalApproved.Amount,
            statement.TotalPaid.Amount,
            statement.TotalReceivable.Amount));
    }

    private Guid RequireTenant() =>
        tenantContext.TenantId ?? throw new DomainRuleException("Estabelecimento não identificado.");
}

/// <summary>Corpo de registro de pagamento.</summary>
public sealed record RegisterPaymentRequest(
    Guid? AppointmentId,
    [Range(0.01, 1_000_000)] decimal Amount,
    PaymentMethod Method);

/// <summary>Corpo de estorno.</summary>
public sealed record RefundRequest([Range(0.01, 1_000_000)] decimal Amount);

/// <summary>Corpo de lançamento.</summary>
public sealed record CreateTransactionRequest(
    TransactionType Type,
    [Range(0.01, 1_000_000)] decimal Amount,
    [Required, MaxLength(300)] string Description,
    Guid? CategoryId = null);

/// <summary>Representação de pagamento.</summary>
public sealed record PaymentResponse(Guid Id, decimal Amount, string Method, string Status);

/// <summary>Representação de lançamento.</summary>
public sealed record TransactionResponse(Guid Id, string Type, decimal Amount, string Description);

/// <summary>Representação de relatório.</summary>
public sealed record ReportResponse(
    DateOnly PeriodStart, DateOnly PeriodEnd,
    decimal TotalIncome, decimal TotalExpense, decimal Balance,
    decimal AverageTicket, int AppointmentCount);

/// <summary>Representação de extrato de comissões.</summary>
public sealed record CommissionStatementResponse(
    Guid ProfessionalId, decimal Pending, decimal Approved, decimal Paid, decimal Receivable);
