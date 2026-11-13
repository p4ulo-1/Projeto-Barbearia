namespace Servia.Domain.Common;

/// <summary>
/// Conflito de concorrência otimista (C10).
///
/// Lançada quando outra transação alterou o mesmo agregado entre a leitura e
/// a gravação. É uma falha esperada sob concorrência — não um defeito — e a
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
