using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servia.Domain.Identity;

namespace Servia.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeamento de <see cref="Invitation"/>.
///
/// O índice único em <c>TokenHash</c> garante busca determinística. O índice
/// em <c>(TenantId, Email)</c> apoia a verificação de convite pendente.
/// </summary>
internal sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Invitations");

        builder.HasKey(invitation => invitation.Id);

        builder.Property(invitation => invitation.Id).ValueGeneratedNever();

        builder.Property(invitation => invitation.TenantId).IsRequired();

        builder.Property(invitation => invitation.Email).HasMaxLength(200).IsRequired();

        builder.Property(invitation => invitation.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(invitation => invitation.TokenHash).HasMaxLength(200).IsRequired();

        builder.Property(invitation => invitation.ExpiresAt).IsRequired();

        builder.Property(invitation => invitation.CreatedAt).IsRequired();

        builder.Property(invitation => invitation.AcceptedAt);

        builder.Property(invitation => invitation.AcceptedByUserId);

        builder.HasIndex(invitation => invitation.TokenHash).IsUnique();

        builder.HasIndex(invitation => new { invitation.TenantId, invitation.Email });

        builder.Ignore(invitation => invitation.IsAccepted);
    }
}
