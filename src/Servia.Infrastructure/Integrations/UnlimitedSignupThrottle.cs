using Servia.Domain.Tenancy;

namespace Servia.Infrastructure.Integrations;

/// <summary>
/// Limite de cadastro desabilitado (F21.3).
///
/// Usado apenas em desenvolvimento: os testes de integração vêm todos do
/// mesmo IP e seriam bloqueados pelo limite, impedindo validar o resto do
/// fluxo.
///
/// <b>Nunca em produção.</b> O registro é condicional ao ambiente.
/// </summary>
public sealed class UnlimitedSignupThrottle : ISignupThrottle
{
    public bool TryAcquire(string clientKey) => true;
}
