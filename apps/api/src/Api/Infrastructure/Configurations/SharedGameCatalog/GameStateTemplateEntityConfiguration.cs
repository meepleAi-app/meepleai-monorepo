using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.SharedGameCatalog;

/// <summary>
/// Entity Framework configuration for GameStateTemplate entity.
/// Issue #2400: GameStateTemplate Entity + AI Generation
/// </summary>
internal sealed class GameStateTemplateEntityConfiguration : IEntityTypeConfiguration<GameStateTemplateEntity>
{
    public void Configure(EntityTypeBuilder<GameStateTemplateEntity> builder)
    {
        builder.ToTable("game_state_templates");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(t => t.SharedGameId)
            .HasColumnName("shared_game_id")
            .IsRequired();

        builder.Property(t => t.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.SchemaJson)
            .HasColumnName("schema_json")
            .HasColumnType("jsonb"); // PostgreSQL JSONB for efficient JSON storage and querying

        builder.Property(t => t.Version)
            .HasColumnName("version")
            .HasMaxLength(20)
            .HasDefaultValue("1.0")
            .IsRequired();

        builder.Property(t => t.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(t => t.Source)
            .HasColumnName("source")
            .HasDefaultValue(0) // AI by default
            .IsRequired();

        builder.Property(t => t.ConfidenceScore)
            .HasColumnName("confidence_score")
            .HasPrecision(5, 4); // 0.0000 to 1.0000

        builder.Property(t => t.GeneratedAt)
            .HasColumnName("generated_at")
            .IsRequired();

        builder.Property(t => t.CreatedBy)
            .HasColumnName("created_by")
            .IsRequired();

        // Indexes for query performance
        builder.HasIndex(t => t.SharedGameId)
            .HasDatabaseName("ix_game_state_templates_shared_game_id");

        builder.HasIndex(t => new { t.SharedGameId, t.IsActive })
            .HasDatabaseName("ix_game_state_templates_shared_game_id_is_active");

        builder.HasIndex(t => new { t.SharedGameId, t.Version })
            .HasDatabaseName("ix_game_state_templates_shared_game_id_version")
            .IsUnique(); // Only one version per game

        // Foreign key to SharedGame
        builder.HasOne(t => t.SharedGame)
            .WithMany()
            .HasForeignKey(t => t.SharedGameId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
