using Api.Infrastructure.Entities.UserNotifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.UserNotifications;

/// <summary>
/// EF Core configuration for NotificationEntity.
/// Configures table mapping, indexes, and constraints.
/// </summary>
internal class NotificationEntityConfiguration : IEntityTypeConfiguration<NotificationEntity>
{
    public void Configure(EntityTypeBuilder<NotificationEntity> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(e => e.Id);

        // Column mappings (explicit for clarity)
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(e => e.Type).HasColumnName("type").HasMaxLength(50).IsRequired();
        builder.Property(e => e.Severity).HasColumnName("severity").HasMaxLength(20).IsRequired();
        builder.Property(e => e.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Message).HasColumnName("message").IsRequired();
        builder.Property(e => e.Link).HasColumnName("link").HasMaxLength(500);
        builder.Property(e => e.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
        builder.Property(e => e.IsRead).HasColumnName("is_read").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.ReadAt).HasColumnName("read_at");
        builder.Property(e => e.CorrelationId).HasColumnName("correlation_id");

        // CorrelationId index for cross-channel tracking
        builder.HasIndex(e => e.CorrelationId)
            .HasDatabaseName("IX_notifications_correlation_id")
            .HasFilter("correlation_id IS NOT NULL");

        // Critical indexes for notification queries (Issue #2053 code review)
        // Primary query: get user notifications ordered by creation date
        builder.HasIndex(e => new { e.UserId, e.CreatedAt })
            .HasDatabaseName("IX_notifications_user_id_created_at")
            .IsDescending(false, true); // user_id ASC, created_at DESC

        // Unread notifications query optimization
        builder.HasIndex(e => new { e.UserId, e.IsRead, e.CreatedAt })
            .HasDatabaseName("IX_notifications_user_id_is_read_created_at")
            .HasFilter("is_read = false"); // Partial index for unread only

        // Unread count query optimization
        builder.HasIndex(e => new { e.UserId, e.IsRead })
            .HasDatabaseName("IX_notifications_user_id_is_read");
    }
}
