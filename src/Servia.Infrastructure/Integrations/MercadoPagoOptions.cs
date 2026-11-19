namespace Servia.Infrastructure.Integrations;

public sealed class MercadoPagoOptions
{
    public string AccessToken { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = string.Empty;
    public Dictionary<string, string> TrialPlanIds { get; set; } = [];

    public bool HasAccessToken => !string.IsNullOrEmpty(AccessToken);

    internal bool HasCheckoutReturnUrl => Uri.TryCreate(ReturnUrl, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && uri.Host.Length > 0 && uri.UserInfo.Length == 0
        && !ReturnUrl.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c is '?' or '#' or '\\');

    public void Validate()
    {
        if (AccessToken is null || (HasAccessToken && !IsBearerToken(AccessToken)))
        {
            throw new InvalidOperationException("MercadoPago:AccessToken tem formato inválido.");
        }

        if (WebhookSecret is null || WebhookSecret.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
        {
            throw new InvalidOperationException("MercadoPago:WebhookSecret tem formato inválido.");
        }

        if (ReturnUrl is null || (ReturnUrl.Length > 0 && !HasCheckoutReturnUrl))
        {
            throw new InvalidOperationException("MercadoPago:ReturnUrl deve ser uma URL HTTPS absoluta sem credenciais, consulta ou fragmento.");
        }

        if (TrialPlanIds is null || TrialPlanIds.Any(plan => string.IsNullOrWhiteSpace(plan.Key)
            || !string.Equals(plan.Key, plan.Key.Trim(), StringComparison.Ordinal)
            || !MercadoPagoGateway.IsExternalId(plan.Value)))
        {
            throw new InvalidOperationException("MercadoPago:TrialPlanIds exige códigos de plano e identificadores válidos.");
        }

        var hasReturnUrl = ReturnUrl.Length > 0;
        var hasTrialPlans = TrialPlanIds.Count > 0;
        var hasWebhookSecret = WebhookSecret.Length > 0;
        if (!HasAccessToken && (hasReturnUrl || hasTrialPlans || hasWebhookSecret))
        {
            throw new InvalidOperationException("MercadoPago:AccessToken é obrigatório quando a integração está configurada.");
        }

        if ((hasReturnUrl || hasTrialPlans) && !(hasReturnUrl && hasTrialPlans && hasWebhookSecret))
        {
            throw new InvalidOperationException("O checkout exige MercadoPago:ReturnUrl, MercadoPago:TrialPlanIds e MercadoPago:WebhookSecret.");
        }
    }

    private static bool IsBearerToken(string token)
    {
        // RFC 6750, seção 2.1. A sintaxe do cabeçalho não identifica o ambiente.
        var value = token.TrimEnd('=');
        return value.Length > 0 && value.All(c => char.IsAsciiLetterOrDigit(c)
            || c is '-' or '.' or '_' or '~' or '+' or '/');
    }
}
