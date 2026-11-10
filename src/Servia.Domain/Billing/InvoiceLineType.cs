namespace Servia.Domain.Billing;

/// <summary>
/// Origem de uma linha de cobrança (F4.14).
///
/// WhatsApp e IA são cobrados em linhas <b>separadas</b>: um recurso nunca
/// encobre o outro na fatura, e o tenant consegue identificar a origem de
/// cada custo (ADR-021).
/// </summary>
public enum InvoiceLineType
{
    /// <summary>Mensalidade do plano contratado.</summary>
    Subscription = 1,

    /// <summary>Consumo de WhatsApp acima da franquia gratuita da Meta.</summary>
    WhatsApp = 2,

    /// <summary>Consumo de IA acima da franquia mensal do plano.</summary>
    Ai = 3,

    /// <summary>Alteração de site solicitada após a publicação.</summary>
    SiteChange = 4,

    /// <summary>Compra antecipada de créditos de consumo.</summary>
    CreditPurchase = 5
}
