using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

internal class UserEntityConfiguration : IEntityTypeConfiguration<UserEntity>
{
    public void Configure(EntityTypeBuilder<UserEntity> builder)
    {
        builder.ToTable("users");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasMaxLength(64);
        builder.Property(e => e.Email).IsRequired().HasMaxLength(256);
        builder.Property(e => e.DisplayName).HasMaxLength(128);
        builder.Property(e => e.PasswordHash).IsRequired(false); // Nullable for OAuth-only users
        builder.Property(e => e.Role)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();

        // User Preferences
        builder.Property(e => e.Language).IsRequired().HasMaxLength(10).HasDefaultValue("en");
        builder.Property(e => e.EmailNotifications).IsRequired().HasDefaultValue(true);
        builder.Property(e => e.Theme).IsRequired().HasMaxLength(20).HasDefaultValue("system");
        builder.Property(e => e.DataRetentionDays).IsRequired().HasDefaultValue(90);

        // AUTH-07: Two-Factor Authentication
        builder.Property(e => e.TotpSecretEncrypted).HasMaxLength(512);
        builder.Property(e => e.IsTwoFactorEnabled).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.TwoFactorEnabledAt);

        // ISSUE-3071: Email Verification
        builder.Property(e => e.EmailVerified).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.EmailVerifiedAt);

        // ISSUE-3141: Gamification (Level/XP)
        builder.Property(e => e.Level).IsRequired().HasDefaultValue(1);
        builder.Property(e => e.ExperiencePoints).IsRequired().HasDefaultValue(0);

        // Relationships
        builder.HasMany(e => e.Sessions)
            .WithOne(s => s.User)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.BackupCodes)
            .WithOne(bc => bc.User)
            .HasForeignKey(bc => bc.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(e => e.Email).IsUnique();
    }
}
