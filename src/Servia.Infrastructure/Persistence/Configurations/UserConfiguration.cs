using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servia.Domain.Identity;

namespace Servia.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeamento de <see cref="User"/>.
///
/// O índice único em <c>(TenantId, Email)</c> reflete a identidade
/// independente por tenant (ADR-015): o e-mail é único dentro do
/// estabelecimento, não globalmente.
/// </summary>
internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Users");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Id).ValueGeneratedNever();

        builder.Property(user => user.TenantId).IsRequired();

        builder.Property(user => user.Name).HasMaxLength(150).IsRequired();

        builder.Property(user => user.Email).HasMaxLength(200).IsRequired();

        builder.Property(user => user.PasswordHash).HasMaxLength(500).IsRequired();

        builder.Property(user => user.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(user => user.CustomRoleId);

        builder.Property(user => user.IsActive).IsRequired();

        builder.Property(user => user.CreatedAt).IsRequired();

        builder.Property(user => user.LastLoginAt);

        // E-mail único POR TENANT, nunca globalmente (ADR-015).
        builder.HasIndex(user => new { user.TenantId, user.Email }).IsUnique();

        builder.Ignore(user => user.IsPlatformAdmin);
        builder.Ignore(user => user.CanSignIn);
    }
}
