using Microsoft.Extensions.Configuration;
using Servia.Infrastructure.Integrations;

namespace Servia.Tests.Integrations;

public sealed class MercadoPagoConfigurationTests
{
    [Fact]
    public void Configuracao_Vazia_Deixa_A_Integracao_Desabilitada()
    {
        var options = Bind([]);
        options.Validate();
        Assert.False(options.HasAccessToken);
    }

    [Theory]
    [InlineData("AccessToken", "bearer invalid-marker", "AccessToken")]
    [InlineData("AccessToken", " ", "AccessToken")]
    [InlineData("AccessToken", "", "AccessToken")]
    [InlineData("AccessToken", null, "AccessToken")]
    [InlineData("WebhookSecret", "invalid-marker\n", "WebhookSecret")]
    [InlineData("WebhookSecret", "", "WebhookSecret")]
    [InlineData("ReturnUrl", "", "ReturnUrl")]
    [InlineData("ReturnUrl", null, "ReturnUrl")]
    [InlineData("ReturnUrl", "/invalid-marker", "ReturnUrl")]
    [InlineData("ReturnUrl", "http://invalid-marker.example", "ReturnUrl")]
    [InlineData("ReturnUrl", "https://user:invalid-marker@servia.example", "ReturnUrl")]
    [InlineData("ReturnUrl", "https://servia.example/return?value=invalid-marker", "ReturnUrl")]
    [InlineData("ReturnUrl", "https://servia.example/return#invalid-marker", "ReturnUrl")]
    [InlineData("TrialPlanIds:PRO", "", "TrialPlanIds")]
    [InlineData("TrialPlanIds:PRO", "../invalid-marker", "TrialPlanIds")]
    public void Configuracao_Invalida_Menciona_A_Chave_Sem_Ecoar_Valores(string key, string? value, string rejectedKey)
    {
        var settings = new Dictionary<string, string?>
        {
            ["AccessToken"] = "fixture-bearer",
            ["WebhookSecret"] = "fixture-secret",
            ["ReturnUrl"] = "https://servia.example/return",
            ["TrialPlanIds:PRO"] = "provider_plan_fixture"
        };
        settings[key] = value;
        var error = Assert.Throws<InvalidOperationException>(() => Bind(settings).Validate());

        Assert.Contains($"MercadoPago:{rejectedKey}", error.Message);
        Assert.DoesNotContain("invalid-marker", error.Message);
        Assert.DoesNotContain("fixture-secret", error.Message);
        Assert.DoesNotContain("fixture-bearer", error.Message);
    }

    [Fact]
    public void Configuracao_Recusa_Url_Sem_Mapeamento_De_Planos()
    {
        var options = Bind(new Dictionary<string, string?>
        {
            ["AccessToken"] = "fixture-bearer",
            ["WebhookSecret"] = "fixture-secret",
            ["ReturnUrl"] = "https://servia.example/return"
        });

        var error = Assert.Throws<InvalidOperationException>(options.Validate);
        Assert.Contains("MercadoPago:TrialPlanIds", error.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Configuracao_Aceita_Operacoes_Sem_Checkout_E_Configuracao_Completa(bool checkout)
    {
        var settings = new Dictionary<string, string?> { ["AccessToken"] = "fixture-bearer" };
        if (checkout)
        {
            settings["WebhookSecret"] = "fixture-secret";
            settings["ReturnUrl"] = "https://servia.example/return";
            settings["TrialPlanIds:PRO"] = "provider_plan_fixture";
        }
        var options = Bind(settings);

        options.Validate();
        Assert.True(options.HasAccessToken);
    }

    private static MercadoPagoOptions Bind(IEnumerable<KeyValuePair<string, string?>> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings.Select(pair =>
            new KeyValuePair<string, string?>($"MercadoPago:{pair.Key}", pair.Value)))
            .Build().GetSection("MercadoPago").Get<MercadoPagoOptions>() ?? new();
}
