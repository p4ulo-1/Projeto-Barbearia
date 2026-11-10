using Microsoft.EntityFrameworkCore;
using Servia.Domain.Common;
using Servia.Domain.Tenancy;

namespace Servia.Infrastructure.Persistence;

/// <summary>
/// Cria os planos comerciais da plataforma (F11.3).
///
/// Os quatro planos definidos no produto: Essencial, Pro, Premium e
/// Business. Todos oferecem todos os módulos; a diferenciação está na
/// quantidade de profissionais, no grau de personalização do site e na
/// franquia de IA (ARCH.md §13.1).
///
/// A operação é idempotente: rodar duas vezes não duplica planos.
/// </summary>
public static class PlanSeeder
{
    /// <summary>
    /// Garante que os planos padrão existam.
    /// Retorna quantos foram criados.
    /// </summary>
    public static async Task<int> SeedAsync(
        AppDbContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var existing = await context.Plans
            .Select(plan => plan.Code)
            .ToListAsync(cancellationToken);

        var created = 0;

        foreach (var definition in Definitions)
        {
            if (existing.Contains(definition.Code, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            context.Plans.Add(Plan.Create(
                definition.Code,
                definition.Name,
                Money.From(definition.MonthlyPrice),
                definition.IncludedProfessionals,
                definition.CustomizationLevel,
                definition.IncludedAiUnits));

            created++;
        }

        if (created > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return created;
    }

    /// <summary>Definição dos planos comerciais.</summary>
    private static readonly PlanDefinition[] Definitions =
    [
        new("ESSENCIAL", "Essencial", 99m, 3, SiteCustomizationLevel.Basic, 100),
        new("PRO", "Pro", 199m, 7, SiteCustomizationLevel.Intermediate, 500),
        new("PREMIUM", "Premium", 299m, 15, SiteCustomizationLevel.Advanced, 1500),
        new("BUSINESS", "Business", 499m, 0, SiteCustomizationLevel.Full, 5000)
    ];

    private sealed record PlanDefinition(
        string Code,
        string Name,
        decimal MonthlyPrice,
        int IncludedProfessionals,
        SiteCustomizationLevel CustomizationLevel,
        int IncludedAiUnits);
}
