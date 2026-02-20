using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.GameManagement;

/// <summary>
/// EF Core configuration for GameReviewEntity.
/// Issue #4904: Game reviews API endpoint.
/// </summary>
internal sealed class GameReviewEntityConfiguration : IEntityTypeConfiguration<GameReviewEntity>
{
    public void Configure(EntityTypeBuilder<GameReviewEntity> builder)
    {
        builder.ToTable("game_reviews");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).IsRequired();

        builder.Property(e => e.SharedGameId)
            .IsRequired();

        builder.Property(e => e.UserId)
            .IsRequired();

        builder.Property(e => e.AuthorName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Rating)
            .IsRequired();

        builder.Property(e => e.Content)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .IsRequired();

        // Enforce one review per user per game at DB level
        builder.HasIndex(e => new { e.SharedGameId, e.UserId })
            .IsUnique()
            .HasDatabaseName("IX_GameReviews_SharedGameId_UserId");

        builder.HasIndex(e => e.SharedGameId)
            .HasDatabaseName("IX_GameReviews_SharedGameId");
    }
}
