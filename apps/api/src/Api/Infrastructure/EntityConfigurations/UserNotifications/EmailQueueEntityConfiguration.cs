using Api.Infrastructure.Entities.UserNotifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.UserNotifications;

/// <summary>
/// EF Core configuration for EmailQueueEntity.
/// Configures table mapping, indexes, and constraints.
/// Issue #4417: Email notification queue with retry policy.
/// </summary>
internal class EmailQueueEntityConfiguration : IEntityTypeConfiguration<EmailQueueEntity>
{
    public void Configure(EntityTypeBuilder<EmailQueueEntity> builder)
    {
        builder.ToTable("email_queue_items");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(e => e.To).HasColumnName("to_address").HasMaxLength(320).IsRequired();
        builder.Property(e => e.Subject).HasColumnName("subject").HasMaxLength(500).IsRequired();
        builder.Property(e => e.HtmlBody).HasColumnName("html_body").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        builder.Property(e => e.RetryCount).HasColumnName("retry_count").IsRequired();
        builder.Property(e => e.MaxRetries).HasColumnName("max_retries").IsRequired().HasDefaultValue(3);
        builder.Property(e => e.NextRetryAt).HasColumnName("next_retry_at");
        builder.Property(e => e.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.ProcessedAt).HasColumnName("processed_at");
        builder.Property(e => e.FailedAt).HasColumnName("failed_at");
        builder.Property(e => e.CorrelationId).HasColumnName("correlation_id");

        // CorrelationId index for cross-channel tracking
        builder.HasIndex(e => e.CorrelationId)
            .HasDatabaseName("IX_email_queue_items_correlation_id")
            .HasFilter("correlation_id IS NOT NULL");

        // Primary index: batch pickup of pending/failed emails ready for retry
        builder.HasIndex(e => new { e.Status, e.NextRetryAt })
            .HasDatabaseName("IX_email_queue_items_status_next_retry_at");

        // User email history (paginated, newest first)
        builder.HasIndex(e => new { e.UserId, e.CreatedAt })
            .HasDatabaseName("IX_email_queue_items_user_id_created_at")
            .IsDescending(false, true);

        // Status monitoring (admin dashboard)
        builder.HasIndex(e => e.Status)
            .HasDatabaseName("IX_email_queue_items_status");
    }
}
