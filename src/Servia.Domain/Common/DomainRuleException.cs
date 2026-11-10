namespace Servia.Domain.Common;

/// <summary>
/// Violação de uma regra de negócio do domínio.
/// Representa uma falha esperada e previsível: o pedido é inválido segundo
/// as regras do negócio, não um defeito do sistema.
/// A camada de API a traduz em resposta 400 com a mensagem de negócio.
/// </summary>
public sealed class DomainRuleException : Exception
{
    public DomainRuleException(string message) : base(message)
    {
    }

    public DomainRuleException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
