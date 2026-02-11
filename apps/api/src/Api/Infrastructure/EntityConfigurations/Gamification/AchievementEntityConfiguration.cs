using Api.Infrastructure.Entities.Gamification;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.Gamification;

/// <summary>
/// EF Core configuration for AchievementEntity.
/// Issue #3922: Achievement System and Badge Engine.
/// </summary>
internal class AchievementEntityConfiguration : IEntityTypeConfiguration<AchievementEntity>
{
    public void Configure(EntityTypeBuilder<AchievementEntity> builder)
    {
        builder.ToTable("achievements");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.IconUrl)
            .HasMaxLength(500);

        builder.Property(e => e.Points)
            .IsRequired();

        builder.Property(e => e.Rarity)
            .IsRequired();

        builder.Property(e => e.Category)
            .IsRequired();

        builder.Property(e => e.Threshold)
            .IsRequired();

        builder.Property(e => e.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        // Unique code index
        builder.HasIndex(e => e.Code)
            .IsUnique()
            .HasDatabaseName("IX_Achievements_Code");

        // Active achievements query
        builder.HasIndex(e => e.IsActive)
            .HasDatabaseName("IX_Achievements_IsActive");

        // Category filter
        builder.HasIndex(e => e.Category)
            .HasDatabaseName("IX_Achievements_Category");
    }
}
