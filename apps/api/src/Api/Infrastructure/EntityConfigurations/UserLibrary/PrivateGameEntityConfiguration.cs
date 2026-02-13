using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.Infrastructure.Entities.UserLibrary;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.UserLibrary;

/// <summary>
/// EF Core configuration for PrivateGameEntity.
/// Issue #3662: Phase 1 - Data Model &amp; Core Infrastructure for Private Games.
/// </summary>
internal class PrivateGameEntityConfiguration : IEntityTypeConfiguration<PrivateGameEntity>
{
    public void Configure(EntityTypeBuilder<PrivateGameEntity> builder)
    {
        builder.ToTable("private_games");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.OwnerId)
            .HasColumnName("owner_id")
            .IsRequired();

        builder.Property(e => e.BggId)
            .HasColumnName("bgg_id");

        builder.Property(e => e.AgentDefinitionId)
            .HasColumnName("agent_definition_id");

        builder.Property(e => e.Title)
            .HasColumnName("title")
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.YearPublished)
            .HasColumnName("year_published");

        builder.Property(e => e.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.Property(e => e.MinPlayers)
            .HasColumnName("min_players")
            .IsRequired();

        builder.Property(e => e.MaxPlayers)
            .HasColumnName("max_players")
            .IsRequired();

        builder.Property(e => e.PlayingTimeMinutes)
            .HasColumnName("playing_time_minutes");

        builder.Property(e => e.MinAge)
            .HasColumnName("min_age");

        builder.Property(e => e.ComplexityRating)
            .HasColumnName("complexity_rating")
            .HasColumnType("decimal(3,2)");

        builder.Property(e => e.ImageUrl)
            .HasColumnName("image_url")
            .HasMaxLength(500);

        builder.Property(e => e.ThumbnailUrl)
            .HasColumnName("thumbnail_url")
            .HasMaxLength(500);

        builder.Property(e => e.Source)
            .HasColumnName("source")
            .IsRequired()
            .HasDefaultValue(PrivateGameSource.Manual);

        builder.Property(e => e.BggSyncedAt)
            .HasColumnName("bgg_synced_at");

        // Audit fields
        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at");

        // Soft delete fields
        builder.Property(e => e.IsDeleted)
            .HasColumnName("is_deleted")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.DeletedAt)
            .HasColumnName("deleted_at");

        // Global query filter for soft deletes
        builder.HasQueryFilter(e => !e.IsDeleted);

        // Indexes
        // Unique index on (OwnerId, BggId) where BggId IS NOT NULL
        // This ensures a user can't have duplicate BGG games
        builder.HasIndex(e => new { e.OwnerId, e.BggId })
            .IsUnique()
            .HasDatabaseName("ix_private_games_owner_bgg")
            .HasFilter("bgg_id IS NOT NULL AND is_deleted = false");

        // Index for querying user's private games
        builder.HasIndex(e => e.OwnerId)
            .HasDatabaseName("ix_private_games_owner_id")
            .HasFilter("is_deleted = false");

        // Index for title search
        builder.HasIndex(e => e.Title)
            .HasDatabaseName("ix_private_games_title")
            .HasFilter("is_deleted = false");

        // Constraints
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("chk_private_games_year_published",
                "year_published IS NULL OR (year_published > 1900 AND year_published <= 2100)");
            t.HasCheckConstraint("chk_private_games_players",
                "min_players > 0 AND max_players >= min_players AND max_players <= 100");
            t.HasCheckConstraint("chk_private_games_playing_time",
                "playing_time_minutes IS NULL OR playing_time_minutes > 0");
            t.HasCheckConstraint("chk_private_games_min_age",
                "min_age IS NULL OR min_age >= 0");
            t.HasCheckConstraint("chk_private_games_complexity",
                "complexity_rating IS NULL OR (complexity_rating >= 1.0 AND complexity_rating <= 5.0)");
        });

        // Relationships
        builder.HasOne(e => e.Owner)
            .WithMany()
            .HasForeignKey(e => e.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.LibraryEntries)
            .WithOne(e => e.PrivateGame)
            .HasForeignKey(e => e.PrivateGameId)
            .OnDelete(DeleteBehavior.SetNull);

        // Issue #4228: Agent linking relationship
        builder.HasOne<AgentDefinition>()
            .WithMany()
            .HasForeignKey(e => e.AgentDefinitionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
