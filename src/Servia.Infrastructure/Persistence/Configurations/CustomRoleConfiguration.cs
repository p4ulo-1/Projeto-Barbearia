using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servia.Domain.Identity;

namespace Servia.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeamento de <see cref="CustomRole"/>.
///
/// O papel personalizado pertence ao tenant (filtro global aplicado por
/// <c>ITenantScoped</c>). As permissões são persistidas como CSV de nomes
/// do enum, o que mantém o schema estável quando novas permissões surgem.
/// </summary>
internal sealed class CustomRoleConfiguration : IEntityTypeConfiguration<CustomRole>
{
    public void Configure(EntityTypeBuilder<CustomRole> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("CustomRoles");

        builder.HasKey(role => role.Id);

        builder.Property(role => role.Id).ValueGeneratedNever();

        builder.Property(role => role.TenantId).IsRequired();

        builder.Property(role => role.Name).HasMaxLength(100).IsRequired();

        builder.Property(role => role.Permissions)
            .HasColumnName("Permissions")
            .HasConversion(
                permissions => string.Join(',', permissions.Select(permission => permission.ToString())),
                text => ParsePermissions(text),
                new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<IReadOnlySet<Permission>>(
                    (left, right) => left.SetEquals(right),
                    permissions => permissions.Aggregate(0, (hash, permission) => hash ^ permission.GetHashCode())))
            .HasMaxLength(1000)
            .IsRequired();

        // Nome único por tenant: evita dois papéis com o mesmo título.
        builder.HasIndex(role => new { role.TenantId, role.Name }).IsUnique();
    }

    private static IReadOnlySet<Permission> ParsePermissions(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new HashSet<Permission>();
        }

        return text
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => Enum.Parse<Permission>(value.Trim()))
            .ToHashSet();
    }
}
