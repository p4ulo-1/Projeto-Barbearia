namespace Servia.Domain.Sites;

/// <summary>
/// Schema dos componentes do site (F3.2).
///
/// Define quais propriedades cada componente aceita e quais são obrigatórias.
/// É a barreira que impede a IA de produzir configuração incompatível com o
/// componente escolhido (ADR-007).
///
/// O schema é fechado de propósito: um componente novo exige uma entrada
/// aqui, o que mantém a biblioteca sob controle do produto.
/// </summary>
public static class SiteComponentSchema
{
    private static readonly IReadOnlyDictionary<SiteComponentType, string[]> Allowed =
        new Dictionary<SiteComponentType, string[]>
        {
            [SiteComponentType.Hero] =
                ["title", "subtitle", "imageUrl", "ctaLabel", "ctaTarget"],

            [SiteComponentType.About] =
                ["title", "body", "imageUrl"],

            [SiteComponentType.Services] =
                ["title", "categoryId", "showPrices", "showDurations"],

            [SiteComponentType.Team] =
                ["title", "showTitles", "showBios"],

            [SiteComponentType.Gallery] =
                ["title", "images", "columns"],

            [SiteComponentType.Testimonials] =
                ["title", "items"],

            [SiteComponentType.Contact] =
                ["title", "address", "phone", "hours", "mapEmbedUrl"],

            [SiteComponentType.BookingWidget] =
                ["title", "serviceId", "professionalId"],

            [SiteComponentType.Faq] =
                ["title", "items"],

            [SiteComponentType.CallToAction] =
                ["title", "body", "ctaLabel", "ctaTarget"]
        };

    private static readonly IReadOnlyDictionary<SiteComponentType, string[]> Required =
        new Dictionary<SiteComponentType, string[]>
        {
            [SiteComponentType.Hero] = ["title"],
            [SiteComponentType.About] = ["title", "body"],
            [SiteComponentType.Services] = ["title"],
            [SiteComponentType.Team] = ["title"],
            [SiteComponentType.Gallery] = ["title"],
            [SiteComponentType.Testimonials] = ["title"],
            [SiteComponentType.Contact] = ["title", "address", "phone"],
            [SiteComponentType.BookingWidget] = ["title"],
            [SiteComponentType.Faq] = ["title"],
            [SiteComponentType.CallToAction] = ["title", "ctaLabel"]
        };

    /// <summary>Propriedades aceitas pelo componente.</summary>
    public static IReadOnlyCollection<string> AllowedKeys(SiteComponentType componentType) =>
        Allowed.TryGetValue(componentType, out var keys) ? keys : [];

    /// <summary>Propriedades obrigatórias do componente.</summary>
    public static IReadOnlyCollection<string> RequiredKeys(SiteComponentType componentType) =>
        Required.TryGetValue(componentType, out var keys) ? keys : [];

    /// <summary>Indica se o tipo de componente existe na biblioteca.</summary>
    public static bool IsSupported(SiteComponentType componentType) =>
        Allowed.ContainsKey(componentType);
}
