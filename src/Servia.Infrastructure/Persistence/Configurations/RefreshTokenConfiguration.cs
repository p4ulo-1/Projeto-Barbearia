using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servia.Domain.Identity;

namespace Servia.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeamento de <see cref="RefreshToken"/>.
///
/// O índice único em <c>TokenHash</c> garante que a busca por token seja
/// determinística e impede duplicatas. O valor em claro nunca é persistido.
/// </summary>
internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("RefreshTokens");

        builder.HasKey(token => token.Id);

        builder.Property(token => token.Id).ValueGeneratedNever();

        builder.Property(token => token.TenantId).IsRequired();

        builder.Property(token => token.UserId).IsRequired();

        builder.Property(token => token.TokenHash).HasMaxLength(200).IsRequired();

        builder.Property(token => token.ExpiresAt).IsRequired();

        builder.Property(token => token.CreatedAt).IsRequired();

        builder.Property(token => token.RevokedAt);

        builder.Property(token => token.RevokedReason).HasMaxLength(200);

        builder.HasIndex(token => token.TokenHash).IsUnique();

        builder.HasIndex(token => new { token.TenantId, token.UserId });

        builder.Ignore(token => token.IsRevoked);
    }
}
