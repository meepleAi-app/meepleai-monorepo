using Api.Infrastructure.Entities.UserNotifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.UserNotifications;

/// <summary>
/// EF Core configuration for NotificationQueueEntity.
/// Configures table mapping, indexes, and constraints for the multi-channel notification queue.
/// </summary>
internal class NotificationQueueEntityConfiguration : IEntityTypeConfiguration<NotificationQueueEntity>
{
    public void Configure(EntityTypeBuilder<NotificationQueueEntity> builder)
    {
        builder.ToTable("notification_queue_items");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.ChannelType).HasColumnName("channel_type").HasMaxLength(20).IsRequired();
        builder.Property(e => e.RecipientUserId).HasColumnName("recipient_user_id");
        builder.Property(e => e.NotificationType).HasColumnName("notification_type").HasMaxLength(50).IsRequired();
        builder.Property(e => e.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.SlackChannelTarget).HasColumnName("slack_channel_target").HasMaxLength(500);
        builder.Property(e => e.SlackTeamId).HasColumnName("slack_team_id").HasMaxLength(20);
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        builder.Property(e => e.RetryCount).HasColumnName("retry_count").IsRequired();
        builder.Property(e => e.MaxRetries).HasColumnName("max_retries").IsRequired().HasDefaultValue(3);
        builder.Property(e => e.NextRetryAt).HasColumnName("next_retry_at");
        builder.Property(e => e.LastError).HasColumnName("last_error").HasMaxLength(2000);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.ProcessedAt).HasColumnName("processed_at");
        builder.Property(e => e.CorrelationId).HasColumnName("correlation_id").IsRequired();

        // CorrelationId index for cross-channel tracking
        builder.HasIndex(e => e.CorrelationId)
            .HasDatabaseName("IX_notification_queue_items_correlation_id");

        // Primary index: batch pickup of pending items ready for processing
        builder.HasIndex(e => new { e.Status, e.NextRetryAt })
            .HasDatabaseName("IX_notification_queue_items_status_next_retry_at");

        // Channel type filtering (admin dashboard)
        builder.HasIndex(e => new { e.ChannelType, e.Status })
            .HasDatabaseName("IX_notification_queue_items_channel_type_status");

        // User notification history
        builder.HasIndex(e => new { e.RecipientUserId, e.CreatedAt })
            .HasDatabaseName("IX_notification_queue_items_recipient_user_id_created_at")
            .IsDescending(false, true);
    }
}
