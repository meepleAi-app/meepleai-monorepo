using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.SharedGameCatalog;

/// <summary>
/// Entity configuration for SharedGameEntity.
/// </summary>
internal class SharedGameEntityConfiguration : IEntityTypeConfiguration<SharedGameEntity>
{
#pragma warning disable MA0051 // Method is too long - EF Core fluent configuration is necessarily verbose
    public void Configure(EntityTypeBuilder<SharedGameEntity> builder)
#pragma warning restore MA0051
    {
        builder.ToTable("shared_games");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.BggId)
            .HasColumnName("bgg_id");

        builder.Property(e => e.AgentDefinitionId)
            .HasColumnName("agent_definition_id");

        builder.Property(e => e.Title)
            .HasColumnName("title")
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.YearPublished)
            .HasColumnName("year_published")
            .IsRequired();

        builder.Property(e => e.Description)
            .HasColumnName("description")
            .IsRequired()
            .HasColumnType("text");

        builder.Property(e => e.MinPlayers)
            .HasColumnName("min_players")
            .IsRequired();

        builder.Property(e => e.MaxPlayers)
            .HasColumnName("max_players")
            .IsRequired();

        builder.Property(e => e.PlayingTimeMinutes)
            .HasColumnName("playing_time_minutes")
            .IsRequired();

        builder.Property(e => e.MinAge)
            .HasColumnName("min_age")
            .IsRequired();

        builder.Property(e => e.ComplexityRating)
            .HasColumnName("complexity_rating")
            .HasColumnType("decimal(3,2)");

        builder.Property(e => e.AverageRating)
            .HasColumnName("average_rating")
            .HasColumnType("decimal(4,2)");

        builder.Property(e => e.ImageUrl)
            .HasColumnName("image_url")
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(e => e.ThumbnailUrl)
            .HasColumnName("thumbnail_url")
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.RulesContent)
            .HasColumnName("rules_content")
            .HasColumnType("text");

        builder.Property(e => e.RulesLanguage)
            .HasColumnName("rules_language")
            .HasMaxLength(10);

        builder.Property(e => e.CreatedBy)
            .HasColumnName("created_by")
            .IsRequired();

        builder.Property(e => e.ModifiedBy)
            .HasColumnName("modified_by");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.ModifiedAt)
            .HasColumnName("modified_at");

        builder.Property(e => e.IsDeleted)
            .HasColumnName("is_deleted")
            .IsRequired()
            .HasDefaultValue(false);

        // Global query filter for soft deletes
        builder.HasQueryFilter(e => !e.IsDeleted);

        // Relationships (Issue #4228)
        builder.HasOne<AgentDefinition>()
            .WithMany()
            .HasForeignKey(e => e.AgentDefinitionId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(e => e.BggId)
            .IsUnique()
            .HasDatabaseName("ix_shared_games_bgg_id")
            .HasFilter("bgg_id IS NOT NULL");

        builder.HasIndex(e => e.Status)
            .HasDatabaseName("ix_shared_games_status")
            .HasFilter("is_deleted = false");

        builder.HasIndex(e => e.Title)
            .HasDatabaseName("ix_shared_games_title")
            .HasFilter("is_deleted = false");

        // Note: SearchVector index will be added manually in migration with tsvector type

        // Constraints
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("chk_shared_games_year_published",
                "year_published > 1900 AND year_published <= 2100");
            t.HasCheckConstraint("chk_shared_games_players",
                "min_players > 0 AND max_players >= min_players");
            t.HasCheckConstraint("chk_shared_games_playing_time",
                "playing_time_minutes > 0");
            t.HasCheckConstraint("chk_shared_games_min_age",
                "min_age >= 0");
            t.HasCheckConstraint("chk_shared_games_complexity",
                "complexity_rating IS NULL OR (complexity_rating >= 1.0 AND complexity_rating <= 5.0)");
            t.HasCheckConstraint("chk_shared_games_rating",
                "average_rating IS NULL OR (average_rating >= 1.0 AND average_rating <= 10.0)");
        });
    }
}
