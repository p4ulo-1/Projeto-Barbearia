using BackAndre.DTOs.Common;
using BackAndre.Models;

namespace BackAndre.Middleware;

public class ExceptionHandlingMiddleware(
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
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new ErrorResponse(exception.Message));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Erro inesperado ao processar {Method} {Path}",
                context.Request.Method, context.Request.Path);

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new ErrorResponse(
                "Ocorreu um erro interno inesperado.",
                TraceId: context.TraceIdentifier));
        }
    }
}
