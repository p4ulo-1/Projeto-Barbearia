using Servia.Api.Controllers;

namespace Servia.Tests.Sites;

/// <summary>
/// Sanitização do nome de arquivo no download (C8).
///
/// O nome vem do cliente; aspas, CR/LF e caracteres de controle permitiriam
/// injetar cabeçalhos. O sanitizador os remove.
/// </summary>
public sealed class ContentDispositionSanitizerTests
{
    [Fact]
    public void Remove_Aspas()
    {
        Assert.Equal("logo.png", OnboardingAssetsController.SanitizeFileName("lo\"go.png"));
    }

    [Fact]
    public void Remove_Quebra_De_Linha()
    {
        var sanitized = OnboardingAssetsController.SanitizeFileName("logo\r\nX-Injected: 1.png");

        // A injecao depende da quebra de linha; sem ela, o texto e inofensivo.
        Assert.DoesNotContain("\r", sanitized);
        Assert.DoesNotContain("\n", sanitized);
    }

    [Fact]
    public void Remove_Barra_Invertida()
    {
        Assert.Equal("logopng", OnboardingAssetsController.SanitizeFileName("logo\\png"));
    }

    [Fact]
    public void Nome_Vazio_Recebe_Padrao()
    {
        Assert.Equal("arquivo", OnboardingAssetsController.SanitizeFileName("   "));
    }

    [Fact]
    public void Nome_Normal_E_Preservado()
    {
        Assert.Equal("foto-da-loja.png", OnboardingAssetsController.SanitizeFileName("foto-da-loja.png"));
    }
}
