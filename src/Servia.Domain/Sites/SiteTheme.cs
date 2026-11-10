using Servia.Domain.Common;

namespace Servia.Domain.Sites;

/// <summary>
/// Identidade visual do site: cores, tipografia e estilo de layout.
///
/// Objeto de valor: duas instâncias com os mesmos valores são equivalentes.
/// As cores são validadas no formato hexadecimal, para que o frontend não
/// receba um valor que não consegue renderizar.
/// </summary>
public readonly record struct SiteTheme
{
    private SiteTheme(
        string primaryColor,
        string secondaryColor,
        string fontFamily,
        string layoutStyle)
    {
        PrimaryColor = primaryColor;
        SecondaryColor = secondaryColor;
        FontFamily = fontFamily;
        LayoutStyle = layoutStyle;
    }

    public string PrimaryColor { get; }

    public string SecondaryColor { get; }

    public string FontFamily { get; }

    public string LayoutStyle { get; }

    /// <summary>Tema padrão, usado quando o tenant ainda não personalizou.</summary>
    public static SiteTheme Default => new("#1F2937", "#F3F4F6", "Inter", "classic");

    public static SiteTheme Create(
        string primaryColor,
        string secondaryColor,
        string fontFamily,
        string layoutStyle)
    {
        EnsureHexColor(primaryColor, nameof(primaryColor));
        EnsureHexColor(secondaryColor, nameof(secondaryColor));

        if (string.IsNullOrWhiteSpace(fontFamily))
        {
            throw new DomainRuleException("A tipografia é obrigatória.");
        }

        if (string.IsNullOrWhiteSpace(layoutStyle))
        {
            throw new DomainRuleException("O estilo de layout é obrigatório.");
        }

        return new SiteTheme(
            primaryColor.Trim().ToUpperInvariant(),
            secondaryColor.Trim().ToUpperInvariant(),
            fontFamily.Trim(),
            layoutStyle.Trim().ToLowerInvariant());
    }

    private static void EnsureHexColor(string color, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            throw new DomainRuleException($"A cor {parameterName} é obrigatória.");
        }

        var trimmed = color.Trim();

        if (trimmed.Length != 7 || trimmed[0] != '#')
        {
            throw new DomainRuleException(
                $"A cor {parameterName} deve estar no formato hexadecimal (#RRGGBB).");
        }

        for (var index = 1; index < trimmed.Length; index++)
        {
            if (!Uri.IsHexDigit(trimmed[index]))
            {
                throw new DomainRuleException(
                    $"A cor {parameterName} deve estar no formato hexadecimal (#RRGGBB).");
            }
        }
    }
}
