using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.SharedGameCatalog;

/// <summary>
/// Entity configuration for SharedGameEntity.
/// </summary>
internal class SharedGameEntityConfiguration : IEntityTypeConfiguration<SharedGameEntity>
{
    public void Configure(EntityTypeBuilder<SharedGameEntity> builder)
    {
        builder.ToTable("shared_games");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(e => e.BggId)
            .HasColumnName("bgg_id");

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

        builder.Property(e => e.SearchVector)
            .HasColumnName("search_vector")
            .HasColumnType("tsvector");

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

        builder.HasIndex(e => e.SearchVector)
            .HasDatabaseName("ix_shared_games_search_vector")
            .HasMethod("gin");

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
