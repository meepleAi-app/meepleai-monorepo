using Api.Infrastructure.Entities.UserLibrary;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.UserLibrary;

/// <summary>
/// EF Core configuration for GameSuggestionEntity.
/// Admin Invitation Flow: game suggestions for invited users.
/// </summary>
internal class GameSuggestionEntityConfiguration : IEntityTypeConfiguration<GameSuggestionEntity>
{
    public void Configure(EntityTypeBuilder<GameSuggestionEntity> builder)
    {
        builder.ToTable("game_suggestions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.UserId)
            .IsRequired();

        builder.Property(e => e.GameId)
            .IsRequired();

        builder.Property(e => e.SuggestedByUserId)
            .IsRequired();

        builder.Property(e => e.Source)
            .HasMaxLength(50)
            .IsRequired(false);

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.IsDismissed)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.IsAccepted)
            .IsRequired()
            .HasDefaultValue(false);

        // Indexes
        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("IX_GameSuggestions_UserId");

        builder.HasIndex(e => new { e.UserId, e.GameId })
            .IsUnique()
            .HasDatabaseName("IX_GameSuggestions_UserId_GameId");

        builder.HasIndex(e => new { e.UserId, e.IsDismissed, e.IsAccepted })
            .HasDatabaseName("IX_GameSuggestions_UserId_Status");

        // Relationships
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.SuggestedByUser)
            .WithMany()
            .HasForeignKey(e => e.SuggestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
