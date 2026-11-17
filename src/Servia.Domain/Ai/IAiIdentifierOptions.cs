namespace Servia.Domain.Ai;

/// <summary>
/// Configuração do identificador opaco (gestão de IA, §4.2). O segredo é
/// exclusivo do backend e nunca é registrado em log.
/// </summary>
public interface IAiIdentifierOptions
{
    /// <summary>Segredo do HMAC.</summary>
    string Secret { get; }

    /// <summary>Versão corrente da chave.</summary>
    string KeyVersion { get; }
}
