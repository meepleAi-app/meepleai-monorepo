using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations;

/// <summary>
/// EF Core configuration for GameFAQEntity.
/// Issue #2028: Backend FAQ system for game-specific FAQs.
/// </summary>
internal class GameFAQConfiguration : IEntityTypeConfiguration<GameFAQEntity>
{
    public void Configure(EntityTypeBuilder<GameFAQEntity> builder)
    {
        builder.ToTable("GameFAQs");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.GameId)
            .IsRequired();

        builder.Property(f => f.Question)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(f => f.Answer)
            .IsRequired()
            .HasMaxLength(5000);

        builder.Property(f => f.Upvotes)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(f => f.CreatedAt)
            .IsRequired();

        builder.Property(f => f.UpdatedAt)
            .IsRequired(false);

        // Optimistic concurrency control (Issue #2028: Code review fix)
        builder.Property(f => f.RowVersion)
            .IsRowVersion();

        // Foreign key relationship
        builder.HasOne(f => f.Game)
            .WithMany()
            .HasForeignKey(f => f.GameId)
            .OnDelete(DeleteBehavior.Cascade);

        // Composite index for efficient queries (ordered by upvotes DESC, then createdAt DESC)
        // Issue #2028: Code review fix - Added CreatedAt to prevent filesort
        builder.HasIndex(f => new { f.GameId, f.Upvotes, f.CreatedAt })
            .HasDatabaseName("IX_GameFAQs_GameId_Upvotes_CreatedAt");
    }
}
