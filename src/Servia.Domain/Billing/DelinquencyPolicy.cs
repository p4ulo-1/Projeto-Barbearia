using Servia.Domain.Common;

namespace Servia.Domain.Billing;

/// <summary>
/// Régua de inadimplência (F4.6, F4.7).
///
/// Define o que acontece quando a cobrança falha, conforme a decisão do
/// produto (ARCH.md §13.2):
///
///   1. Trial de 14 dias com cartão cadastrado.
///   2. Ao fim do trial, o cartão é debitado automaticamente.
///   3. Inadimplência: 3 dias de aviso com acesso total.
///   4. Depois: bloqueio de escrita, com leitura e agenda por 15 dias.
///   5. Após 15 dias de bloqueio: suspensão total.
///
/// A régua é uma função pura do tempo: dado o instante da falha e o instante
/// atual, devolve o estado esperado. Isso a torna testável sem esperar dias.
/// </summary>
public static class DelinquencyPolicy
{
    /// <summary>Dias de aviso com acesso total após a falha.</summary>
    public const int GraceDays = 3;

    /// <summary>Dias de bloqueio de escrita com leitura mantida.</summary>
    public const int ReadOnlyDays = 15;

    /// <summary>
    /// Calcula o estado de acesso do tenant a partir da falha de cobrança.
    /// </summary>
    public static DelinquencyState Evaluate(DateTimeOffset failedAt, DateTimeOffset now)
    {
        if (now < failedAt)
        {
            throw new DomainRuleException("O instante atual não pode ser anterior à falha.");
        }

        var elapsed = now - failedAt;

        if (elapsed.TotalDays < GraceDays)
        {
            return DelinquencyState.Warning;
        }

        if (elapsed.TotalDays < GraceDays + ReadOnlyDays)
        {
            return DelinquencyState.WriteBlocked;
        }

        return DelinquencyState.Suspended;
    }

    /// <summary>Indica se o tenant pode escrever no estado informado.</summary>
    public static bool CanWrite(DelinquencyState state) =>
        state == DelinquencyState.Warning;

    /// <summary>Indica se o tenant pode ler no estado informado.</summary>
    public static bool CanRead(DelinquencyState state) =>
        state != DelinquencyState.Suspended;

    /// <summary>
    /// Data em que o bloqueio de escrita começa, a partir da falha.
    /// </summary>
    public static DateTimeOffset WriteBlockStartsAt(DateTimeOffset failedAt) =>
        failedAt.AddDays(GraceDays);

    /// <summary>
    /// Data em que a suspensão total começa, a partir da falha.
    /// </summary>
    public static DateTimeOffset SuspensionStartsAt(DateTimeOffset failedAt) =>
        failedAt.AddDays(GraceDays + ReadOnlyDays);
}

/// <summary>Estado de acesso durante a inadimplência.</summary>
public enum DelinquencyState
{
    /// <summary>Aviso: acesso total, com comunicação ao tenant.</summary>
    Warning = 1,

    /// <summary>Bloqueio de escrita: leitura e agenda mantidas.</summary>
    WriteBlocked = 2,

    /// <summary>Suspensão total.</summary>
    Suspended = 3
}
