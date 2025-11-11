using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

// EDIT-05: Enhanced Comments System
public class RuleSpecCommentEntityConfiguration : IEntityTypeConfiguration<RuleSpecCommentEntity>
{
    public void Configure(EntityTypeBuilder<RuleSpecCommentEntity> builder)
    {
        builder.ToTable("rulespec_comments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.GameId).IsRequired().HasMaxLength(64);
        builder.Property(e => e.Version).IsRequired().HasMaxLength(32);
        builder.Property(e => e.AtomId).HasMaxLength(64);
        builder.Property(e => e.UserId).IsRequired().HasMaxLength(64);
        builder.Property(e => e.CommentText).IsRequired().HasMaxLength(2000);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt);

        // EDIT-05: Inline Annotations
        builder.Property(e => e.LineNumber);
        builder.Property(e => e.LineContext).HasMaxLength(500);

        // EDIT-05: Comment Threading
        builder.Property(e => e.ParentCommentId);

        // EDIT-05: Resolution Tracking
        builder.Property(e => e.IsResolved).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.ResolvedByUserId).HasMaxLength(64);
        builder.Property(e => e.ResolvedAt);

        // EDIT-05: User Mentions (stored as JSON array)
        builder.Property(e => e.MentionedUserIds)
            .HasConversion(
                v => string.Join(',', v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                      .Select(Guid.Parse)
                      .ToList())
            .HasMaxLength(1000);

        // Relationships
        builder.HasOne(e => e.Game)
            .WithMany()
            .HasForeignKey(e => e.GameId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // EDIT-05: Threading Relationship (self-referencing)
        builder.HasOne(e => e.ParentComment)
            .WithMany(p => p.Replies)
            .HasForeignKey(e => e.ParentCommentId)
            .OnDelete(DeleteBehavior.Restrict);

        // EDIT-05: Resolution Relationship
        builder.HasOne(e => e.ResolvedByUser)
            .WithMany()
            .HasForeignKey(e => e.ResolvedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(e => new { e.GameId, e.Version });
        builder.HasIndex(e => e.AtomId);

        // EDIT-05: Performance indexes for new features
        builder.HasIndex(e => new { e.GameId, e.Version, e.LineNumber })
            .HasDatabaseName("idx_rulespec_comments_game_version_line");
        builder.HasIndex(e => e.ParentCommentId)
            .HasDatabaseName("idx_rulespec_comments_parent_id");
        builder.HasIndex(e => e.IsResolved)
            .HasDatabaseName("idx_rulespec_comments_is_resolved");
        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("idx_rulespec_comments_user_id");
    }
}