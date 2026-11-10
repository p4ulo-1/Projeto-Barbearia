using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servia.Domain.Messaging;

namespace Servia.Infrastructure.Persistence.Configurations;

/// <summary>Mapeamento de <see cref="MessageTemplate"/>.</summary>
internal sealed class MessageTemplateConfiguration : IEntityTypeConfiguration<MessageTemplate>
{
    public void Configure(EntityTypeBuilder<MessageTemplate> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("MessageTemplates");
        builder.HasKey(template => template.Id);
        builder.Property(template => template.Id).ValueGeneratedNever();
        builder.Property(template => template.TenantId).IsRequired();
        builder.Property(template => template.Key).HasMaxLength(100).IsRequired();
        builder.Property(template => template.Channel).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(template => template.MessageType).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(template => template.Body).HasMaxLength(2000).IsRequired();
        builder.Property(template => template.IsActive).IsRequired();

        builder.Ignore(template => template.Variables);

        // Chave unica por tenant e canal.
        builder.HasIndex(template => new { template.TenantId, template.Key, template.Channel }).IsUnique();
    }
}

/// <summary>Mapeamento de <see cref="Notification"/>.</summary>
internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Notifications");
        builder.HasKey(notification => notification.Id);
        builder.Property(notification => notification.Id).ValueGeneratedNever();
        builder.Property(notification => notification.TenantId).IsRequired();
        builder.Property(notification => notification.CustomerId);
        builder.Property(notification => notification.AppointmentId);
        builder.Property(notification => notification.Channel).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(notification => notification.MessageType).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(notification => notification.Recipient).HasMaxLength(200).IsRequired();
        builder.Property(notification => notification.Body).HasMaxLength(2000).IsRequired();
        builder.Property(notification => notification.ScheduledAt);
        builder.Property(notification => notification.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(notification => notification.SentAt);
        builder.Property(notification => notification.FailureReason).HasMaxLength(300);

        builder.Ignore(notification => notification.IsSent);

        // Fila de envio: consulta por status e agendamento.
        builder.HasIndex(notification => new { notification.TenantId, notification.Status, notification.ScheduledAt });
    }
}

/// <summary>Mapeamento de <see cref="Conversation"/> e suas mensagens.</summary>
internal sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Conversations");
        builder.HasKey(conversation => conversation.Id);
        builder.Property(conversation => conversation.Id).ValueGeneratedNever();
        builder.Property(conversation => conversation.TenantId).IsRequired();
        builder.Property(conversation => conversation.CustomerId);
        builder.Property(conversation => conversation.PhoneNumber).HasMaxLength(20).IsRequired();
        builder.Property(conversation => conversation.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(conversation => conversation.AssignedTo);
        builder.Property(conversation => conversation.StartedAt).IsRequired();
        builder.Property(conversation => conversation.ClosedAt);

        builder.OwnsMany(conversation => conversation.Messages, messages =>
        {
            messages.ToTable("ConversationMessages");
            messages.WithOwner().HasForeignKey("ConversationId");
            messages.Property(message => message.Id).ValueGeneratedNever();
            messages.Property(message => message.Direction).HasConversion<string>().HasMaxLength(20).IsRequired();
            messages.Property(message => message.Body).HasMaxLength(4000).IsRequired();
            messages.Property(message => message.SentAt).IsRequired();
            messages.Property(message => message.ExternalId).HasMaxLength(100);
            messages.HasKey(message => message.Id);
        });

        builder.Ignore(conversation => conversation.IsOpen);
        builder.Ignore(conversation => conversation.IsWithHuman);

        builder.HasIndex(conversation => new { conversation.TenantId, conversation.Status });
        builder.HasIndex(conversation => new { conversation.TenantId, conversation.PhoneNumber });
    }
}

/// <summary>Mapeamento de <see cref="MessagingPolicy"/>.</summary>
internal sealed class MessagingPolicyConfiguration : IEntityTypeConfiguration<MessagingPolicy>
{
    public void Configure(EntityTypeBuilder<MessagingPolicy> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("MessagingPolicies");
        builder.HasKey(policy => policy.Id);
        builder.Property(policy => policy.Id).ValueGeneratedNever();
        builder.Property(policy => policy.TenantId).IsRequired();

        // Tipos autorizados: persistidos como CSV de nomes do enum.
        builder.Property(policy => policy.AllowedTypes)
            .HasConversion(
                types => string.Join(',', types.Select(type => type.ToString())),
                text => ParseTypes(text))
            .HasColumnName("AllowedTypes")
            .HasMaxLength(300)
            .IsRequired();

        // Uma politica por tenant.
        builder.HasIndex(policy => policy.TenantId).IsUnique();
    }

    private static IReadOnlyCollection<MessageType> ParseTypes(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => Enum.Parse<MessageType>(value))
            .ToArray();
    }
}
