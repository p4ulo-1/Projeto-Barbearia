using System.Diagnostics;
using Servia.Api.Contracts;
using Servia.Domain.Common;

namespace Servia.Api.Middleware;

/// <summary>
/// Tratamento central de exceções da API.
///
/// Concentra em um único ponto a tradução de exceções em respostas HTTP,
/// evitando que cada controller repita try/catch. Distingue três naturezas
/// de falha (ver docs/av2/especificacao.md §5):
///
///   S1 — <see cref="DomainRuleException"/>  → 400, mensagem de negócio
///   S2 — <see cref="NotFoundException"/>    → 404, mensagem genérica
///   S3 — qualquer outra exceção             → 500, sem vazar detalhe interno
///
/// A falha inesperada é registrada em log com o stack trace completo; o cliente
/// recebe apenas um <c>TraceId</c> para correlação.
/// </summary>
public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DomainRuleException exception)
        {
            await WriteAsync(context, StatusCodes.Status400BadRequest,
                ErrorResponse.FromRuleViolation(exception.Message));
        }
        catch (NotFoundException exception)
        {
            // O detalhe fica no log; o cliente recebe mensagem genérica.
            logger.LogInformation(
                "Recurso não encontrado em {Method} {Path}: {Message}",
                context.Request.Method,
                context.Request.Path,
                exception.Message);

            await WriteAsync(context, StatusCodes.Status404NotFound,
                ErrorResponse.FromNotFound());
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Cancelamento solicitado pelo cliente não é falha do servidor.
            logger.LogInformation(
                "Requisição cancelada pelo cliente em {Method} {Path}",
                context.Request.Method,
                context.Request.Path);
        }
        catch (Exception exception)
        {
            var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

            logger.LogError(
                exception,
                "Falha inesperada em {Method} {Path}. TraceId: {TraceId}",
                context.Request.Method,
                context.Request.Path,
                traceId);

            await WriteAsync(context, StatusCodes.Status500InternalServerError,
                ErrorResponse.FromUnexpected(traceId));
        }
    }

    private static async Task WriteAsync(HttpContext context, int statusCode, ErrorResponse body)
    {
        // Se a resposta já começou, não é possível reescrever o status.
        // Nesse caso, apenas encerra para não corromper o corpo enviado.
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        await context.Response.WriteAsJsonAsync(body);
    }
}

/// <summary>
/// Extensão para registrar o middleware no pipeline.
/// </summary>
public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder app) =>
        app.UseMiddleware<ExceptionHandlingMiddleware>();
}
