namespace Servia.Domain.Common;

/// <summary>
/// Conflito de concorrência otimista ou de serialização transacional.
///
/// Lançada quando outra transação impede a confirmação segura de uma alteração.
/// É uma falha esperada sob concorrência e a
/// camada de API a traduz em <c>409 Conflict</c>.
///
/// Vive no domínio para que a aplicação não dependa do EF Core: a
/// infraestrutura traduz <c>DbUpdateConcurrencyException</c> para este tipo.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message) : base(message)
    {
    }

    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
