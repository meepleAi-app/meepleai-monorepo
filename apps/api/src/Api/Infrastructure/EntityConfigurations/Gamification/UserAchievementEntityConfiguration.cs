using Api.Infrastructure.Entities.Gamification;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.Gamification;

/// <summary>
/// EF Core configuration for UserAchievementEntity.
/// Issue #3922: Achievement System and Badge Engine.
/// </summary>
internal class UserAchievementEntityConfiguration : IEntityTypeConfiguration<UserAchievementEntity>
{
    public void Configure(EntityTypeBuilder<UserAchievementEntity> builder)
    {
        builder.ToTable("user_achievements");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.UserId)
            .IsRequired();

        builder.Property(e => e.AchievementId)
            .IsRequired();

        builder.Property(e => e.Progress)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.UnlockedAt)
            .IsRequired(false);

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .IsRequired(false);

        // User's achievements query
        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("IX_UserAchievements_UserId");

        // Unique: one record per user per achievement
        builder.HasIndex(e => new { e.UserId, e.AchievementId })
            .IsUnique()
            .HasDatabaseName("IX_UserAchievements_UserId_AchievementId");

        // Recent unlocked query (userId + unlockedAt DESC)
        builder.HasIndex(e => new { e.UserId, e.UnlockedAt })
            .HasDatabaseName("IX_UserAchievements_UserId_UnlockedAt");

        // Relationships
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Achievement)
            .WithMany(a => a.UserAchievements)
            .HasForeignKey(e => e.AchievementId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
