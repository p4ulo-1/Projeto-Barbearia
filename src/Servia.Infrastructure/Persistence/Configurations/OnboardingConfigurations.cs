using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servia.Domain.Ai;
using Servia.Domain.Sites;

namespace Servia.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeamento de <see cref="OnboardingSession"/> (itens 1 e 2).
///
/// O histórico da conversa e os fatos coletados são parte da sessão. Ambos
/// são persistidos, para que o proprietário retome o processo sem perder o
/// que já respondeu — é o que torna a conversa retomável.
/// </summary>
internal sealed class OnboardingSessionConfiguration : IEntityTypeConfiguration<OnboardingSession>
{
    public void Configure(EntityTypeBuilder<OnboardingSession> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("OnboardingSessions");
        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id).ValueGeneratedNever();
        builder.Property(session => session.TenantId).IsRequired();
        builder.Property(session => session.BusinessName).HasMaxLength(200).IsRequired();
        builder.Property(session => session.Segment).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(session => session.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(session => session.StartedAt).IsRequired();
        builder.Property(session => session.LastActivityAt);
        builder.Property(session => session.GeneratedChangeSetId);
        builder.Property(session => session.ApprovedBy);
        builder.Property(session => session.ApprovedAt);

        builder.Ignore(session => session.IsOpen);

        // Fatos coletados: dicionario chave-valor persistido como JSON.
        //
        // O comparador informa ao EF como detectar mudanca num dicionario
        // mutado em memoria. Sem ele, adicionar um fato nao marca a entidade
        // como alterada e a alteracao se perde no SaveChanges.
        builder.Property(session => session.CollectedFacts)
            .HasConversion(
                facts => System.Text.Json.JsonSerializer.Serialize(facts, (System.Text.Json.JsonSerializerOptions?)null),
                json => DeserializeFacts(json))
            .Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<IReadOnlyDictionary<string, string>>(
                (left, right) => left!.Count == right!.Count
                    && !left.Except(right).Any(),
                facts => facts.Aggregate(0, (hash, pair) => HashCode.Combine(hash, pair.Key, pair.Value)),
                facts => new Dictionary<string, string>(facts)));
        builder.Property(session => session.CollectedFacts)
            .HasColumnName("CollectedFacts")
            .IsRequired();

        // Historico da conversa: tabela filha, ordenada por envio.
        builder.OwnsMany(session => session.Messages, messages =>
        {
            messages.ToTable("OnboardingMessages");
            messages.WithOwner().HasForeignKey("OnboardingSessionId");
            messages.Property(message => message.Id).ValueGeneratedNever();
            messages.Property(message => message.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
            messages.Property(message => message.Content).HasMaxLength(4000).IsRequired();
            messages.Property(message => message.SentAt).IsRequired();
            messages.HasKey(message => message.Id);
            messages.HasIndex(message => new { message.SentAt });
        });

        builder.HasIndex(session => new { session.TenantId, session.Status });
    }

    private static IReadOnlyDictionary<string, string> DeserializeFacts(string json) =>
        System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
}

/// <summary>
/// Mapeamento de <see cref="AiChangeSet"/> (proposta de site gerada pela IA).
///
/// A proposta é o rascunho que aguarda aprovação. Persistida por sessão:
/// cada geração ou ajuste cria uma nova proposta, preservando o histórico.
/// </summary>
internal sealed class AiChangeSetConfiguration : IEntityTypeConfiguration<AiChangeSet>
{
    public void Configure(EntityTypeBuilder<AiChangeSet> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AiChangeSets");
        builder.HasKey(changeSet => changeSet.Id);
        builder.Property(changeSet => changeSet.Id).ValueGeneratedNever();
        builder.Property(changeSet => changeSet.TenantId).IsRequired();
        builder.Property(changeSet => changeSet.Summary).HasMaxLength(500).IsRequired();

        builder.ComplexProperty(changeSet => changeSet.Theme, theme =>
        {
            theme.Property(value => value.PrimaryColor).HasColumnName("Theme_Primary").HasMaxLength(7).IsRequired();
            theme.Property(value => value.SecondaryColor).HasColumnName("Theme_Secondary").HasMaxLength(7).IsRequired();
            theme.Property(value => value.FontFamily).HasColumnName("Theme_Font").HasMaxLength(100).IsRequired();
            theme.Property(value => value.LayoutStyle).HasColumnName("Theme_Layout").HasMaxLength(50).IsRequired();
        });

        builder.OwnsMany(changeSet => changeSet.Sections, sections =>
        {
            sections.ToTable("AiChangeSetSections");
            sections.WithOwner().HasForeignKey("ChangeSetId");
            sections.Property<int>("Id").ValueGeneratedOnAdd();
            sections.Property(section => section.ComponentType).HasConversion<string>().HasMaxLength(30).IsRequired();
            sections.Property(section => section.SortOrder).IsRequired();
            sections.Property(section => section.Configuration)
                .HasConversion(
                    config => System.Text.Json.JsonSerializer.Serialize(config, (System.Text.Json.JsonSerializerOptions?)null),
                    json => DeserializeConfiguration(json))
                .HasColumnName("Configuration")
                .IsRequired();
            sections.HasKey("Id");
        });
    }

    private static IReadOnlyDictionary<string, string> DeserializeConfiguration(string json) =>
        System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
}

/// <summary>
/// Mapeamento de <see cref="SiteAsset"/> (arquivos enviados pelo proprietário).
///
/// O caminho de armazenamento é prefixado pelo tenant. A consulta é filtrada
/// pelo contexto global, mas o índice por tenant reforça o isolamento.
/// </summary>
internal sealed class SiteAssetConfiguration : IEntityTypeConfiguration<SiteAsset>
{
    public void Configure(EntityTypeBuilder<SiteAsset> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("SiteAssets");
        builder.HasKey(asset => asset.Id);
        builder.Property(asset => asset.Id).ValueGeneratedNever();
        builder.Property(asset => asset.TenantId).IsRequired();
        builder.Property(asset => asset.OnboardingSessionId);
        builder.Property(asset => asset.Kind).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(asset => asset.FileName).HasMaxLength(260).IsRequired();
        builder.Property(asset => asset.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(asset => asset.SizeInBytes).IsRequired();
        builder.Property(asset => asset.StoragePath).HasMaxLength(500).IsRequired();
        builder.Property(asset => asset.Checksum).HasMaxLength(128).IsRequired();
        builder.Property(asset => asset.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(asset => asset.UploadedAt).IsRequired();
        builder.Property(asset => asset.RejectionReason).HasMaxLength(300);

        builder.Ignore(asset => asset.IsUsable);

        builder.HasIndex(asset => new { asset.TenantId, asset.OnboardingSessionId });
    }
}

/// <summary>
/// Mapeamento de <see cref="PublicBookingAttempt"/> (C3).
///
/// O índice único (TenantId, ClientKey, WindowStart) é o que permite a
/// atualização atômica via <c>INSERT ... ON CONFLICT DO UPDATE</c>: duas
/// instâncias concorrentes disputam a mesma linha e o banco serializa.
/// </summary>
internal sealed class PublicBookingAttemptConfiguration : IEntityTypeConfiguration<PublicBookingAttempt>
{
    public void Configure(EntityTypeBuilder<PublicBookingAttempt> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("PublicBookingAttempts");
        builder.HasKey(attempt => attempt.Id);
        builder.Property(attempt => attempt.Id).ValueGeneratedNever();
        builder.Property(attempt => attempt.TenantId).IsRequired();
        builder.Property(attempt => attempt.ClientKey).HasMaxLength(64).IsRequired();
        builder.Property(attempt => attempt.Count).IsRequired();
        builder.Property(attempt => attempt.WindowStart).IsRequired();
        builder.Property(attempt => attempt.WindowEnd).IsRequired();

        builder.HasIndex(attempt => new { attempt.TenantId, attempt.ClientKey, attempt.WindowStart })
            .IsUnique();

        // Indice para a limpeza de janelas expiradas.
        builder.HasIndex(attempt => attempt.WindowEnd);
    }
}

/// <summary>
/// Mapeamento de <see cref="AiSiteChange"/> (C12).
///
/// Alteração direcionada: páginas e campos são owned collections. O status é
/// persistido como texto para legibilidade.
/// </summary>
internal sealed class AiSiteChangeConfiguration : IEntityTypeConfiguration<AiSiteChange>
{
    public void Configure(EntityTypeBuilder<AiSiteChange> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AiSiteChanges");
        builder.HasKey(change => change.Id);
        builder.Property(change => change.Id).ValueGeneratedNever();
        builder.Property(change => change.TenantId).IsRequired();
        builder.Property(change => change.Request).HasMaxLength(2000).IsRequired();
        builder.Property(change => change.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(change => change.CreatedAt).IsRequired();
        builder.Property(change => change.DecidedAt);

        builder.OwnsMany(change => change.Pages, pages =>
        {
            pages.ToTable("AiSiteChangePages");
            pages.WithOwner().HasForeignKey("ChangeId");
            pages.Property<int>("Id").ValueGeneratedOnAdd();
            pages.Property(page => page.PageSlug).HasMaxLength(100).IsRequired();
            pages.HasKey("Id");

            pages.OwnsMany(page => page.Fields, fields =>
            {
                fields.ToTable("AiSiteChangeFields");
                fields.WithOwner().HasForeignKey("PageId");
                fields.Property<int>("Id").ValueGeneratedOnAdd();
                fields.Property(field => field.ComponentType).HasConversion<string>().HasMaxLength(30).IsRequired();
                fields.Property(field => field.FieldKey).HasMaxLength(100).IsRequired();
                fields.Property(field => field.CurrentValue).HasMaxLength(2000);
                fields.Property(field => field.ProposedValue).HasMaxLength(2000).IsRequired();
                fields.HasKey("Id");
            });
        });

        builder.HasIndex(change => new { change.TenantId, change.Status });
    }
}
