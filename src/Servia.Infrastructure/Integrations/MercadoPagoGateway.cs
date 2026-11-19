using Microsoft.Extensions.Logging;
using Servia.Domain.Billing;

namespace Servia.Infrastructure.Integrations;

public sealed partial class MercadoPagoGateway : IRecurringCheckoutGateway
{
    private const string ApiBaseUrl = "https://api.mercadopago.com";

    private readonly HttpClient _httpClient;
    private readonly ILogger<MercadoPagoGateway> _logger;
    private readonly string _returnUrl;
    private readonly IReadOnlyDictionary<string, string> _trialPlanIds;

    public MercadoPagoGateway(HttpClient httpClient, MercadoPagoOptions options, ILogger<MercadoPagoGateway> logger)
    {
        options.Validate();
        if (!options.HasAccessToken)
        {
            throw new InvalidOperationException("O token de acesso do Mercado Pago é obrigatório.");
        }

        _httpClient = httpClient;
        _logger = logger;
        _returnUrl = options.ReturnUrl;
        CanStartCheckout = options.HasCheckoutReturnUrl;
        _trialPlanIds = options.TrialPlanIds;

        _httpClient.BaseAddress ??= new Uri(ApiBaseUrl);
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.AccessToken);
    }

    public bool CanStartCheckout { get; }
}
