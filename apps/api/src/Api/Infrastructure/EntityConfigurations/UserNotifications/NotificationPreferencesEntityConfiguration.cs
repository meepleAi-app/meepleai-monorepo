using Api.Infrastructure.Entities.UserNotifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.UserNotifications;

internal class NotificationPreferencesEntityConfiguration : IEntityTypeConfiguration<NotificationPreferencesEntity>
{
    public void Configure(EntityTypeBuilder<NotificationPreferencesEntity> builder)
    {
        builder.ToTable("notification_preferences");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.UserId).IsRequired();
        builder.HasIndex(e => e.UserId).IsUnique();
        builder.Property(e => e.EmailOnDocumentReady).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.EmailOnDocumentFailed).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.EmailOnRetryAvailable).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.PushOnDocumentReady).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.PushOnDocumentFailed).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.PushOnRetryAvailable).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.PushEndpoint).HasMaxLength(2048);
        builder.Property(e => e.PushP256dhKey).HasMaxLength(512);
        builder.Property(e => e.PushAuthKey).HasMaxLength(512);
        builder.Property(e => e.InAppOnDocumentReady).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.InAppOnDocumentFailed).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.InAppOnRetryAvailable).IsRequired().HasDefaultValue(true);

        // Slack notification preferences
        builder.Property(e => e.SlackEnabled).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.SlackOnDocumentReady).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.SlackOnDocumentFailed).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.SlackOnRetryAvailable).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.SlackOnGameNightInvitation).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.SlackOnGameNightReminder).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.SlackOnShareRequestCreated).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.SlackOnShareRequestApproved).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.SlackOnBadgeEarned).IsRequired().HasDefaultValue(true);
    }
}
