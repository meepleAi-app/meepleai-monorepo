using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

/// <summary>
/// EF Core configuration for RuleConflictFAQEntity.
/// Issue #3761: Conflict Resolution FAQ System.
/// </summary>
internal sealed class RuleConflictFAQEntityConfiguration : IEntityTypeConfiguration<RuleConflictFAQEntity>
{
    public void Configure(EntityTypeBuilder<RuleConflictFAQEntity> builder)
    {
        builder.ToTable("rule_conflict_faqs");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).IsRequired();

        // Foreign key to Game
        builder.Property(e => e.GameId).IsRequired();
        builder.HasOne(e => e.Game)
            .WithMany()
            .HasForeignKey(e => e.GameId)
            .OnDelete(DeleteBehavior.Cascade);

        // Conflict metadata
        builder.Property(e => e.ConflictType)
            .IsRequired();

        builder.Property(e => e.Pattern)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Resolution)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(e => e.Priority)
            .IsRequired();

        builder.Property(e => e.UsageCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .IsRequired();

        // Unique index on GameId + Pattern for fast FAQ lookups
        builder.HasIndex(e => new { e.GameId, e.Pattern })
            .IsUnique()
            .HasDatabaseName("IX_RuleConflictFAQs_GameId_Pattern");

        // Index on UsageCount for analytics (most used FAQs)
        builder.HasIndex(e => e.UsageCount)
            .HasDatabaseName("IX_RuleConflictFAQs_UsageCount");
    }
}
