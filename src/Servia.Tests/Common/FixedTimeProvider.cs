namespace Servia.Tests.Common;

/// <summary>
/// Provedor de tempo fixo para testes determinísticos.
///
/// Definido uma única vez: seis arquivos declaravam a própria cópia, o que
/// é duplicação real e dificulta a manutenção.
/// </summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
