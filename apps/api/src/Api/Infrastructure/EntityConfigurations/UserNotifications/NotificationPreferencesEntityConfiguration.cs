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
        builder.Property(e => e.InAppOnDocumentReady).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.InAppOnDocumentFailed).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.InAppOnRetryAvailable).IsRequired().HasDefaultValue(true);
    }
}
