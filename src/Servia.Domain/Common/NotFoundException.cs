namespace Servia.Domain.Common;

/// <summary>
/// Recurso solicitado não existe.
///
/// Representa uma situação excepcional <b>esperada</b>: o pedido é válido, mas o
/// recurso referenciado não foi encontrado. Diferente de <see cref="DomainRuleException"/>,
/// que indica violação de uma regra de negócio.
///
/// A camada de API a traduz em resposta 404 com mensagem genérica, sem expor
/// detalhes internos.
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }

    public NotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Cria a exceção para um recurso identificado por tipo e chave.
    /// </summary>
    public static NotFoundException For(string resourceName, object key) =>
        new($"{resourceName} não encontrado: {key}.");
}
