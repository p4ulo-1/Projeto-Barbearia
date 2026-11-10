using Microsoft.AspNetCore.Mvc;
using Servia.Domain.Common;

namespace Servia.Api.Controllers;

/// <summary>
/// Endpoints de diagnóstico do tratamento de exceções.
///
/// Existem para tornar as três situações de falha reproduzíveis e testáveis
/// (ver docs/av2/especificacao.md §5). Não expõem dados de negócio e ficam
/// restritos ao ambiente de desenvolvimento.
/// </summary>
[ApiController]
[Route("api/diagnostics")]
public sealed class DiagnosticsController(IHostEnvironment environment) : ControllerBase
{
    /// <summary>S1 — violação de regra de negócio. Deve responder 400.</summary>
    [HttpGet("rule-violation")]
    public IActionResult RuleViolation()
    {
        EnsureDevelopment();

        throw new DomainRuleException("O horário escolhido não está disponível.");
    }

    /// <summary>S2 — recurso inexistente. Deve responder 404.</summary>
    [HttpGet("not-found")]
    public IActionResult NotFoundResource()
    {
        EnsureDevelopment();

        throw NotFoundException.For("Agendamento", Guid.Empty);
    }

    /// <summary>S3 — falha inesperada. Deve responder 500 sem vazar detalhe.</summary>
    [HttpGet("unexpected")]
    public IActionResult Unexpected()
    {
        EnsureDevelopment();

        throw new InvalidOperationException(
            "Detalhe interno que NUNCA deve aparecer na resposta: connection string secreta.");
    }

    private void EnsureDevelopment()
    {
        if (!environment.IsDevelopment())
        {
            throw NotFoundException.For("Recurso", "diagnostics");
        }
    }
}
