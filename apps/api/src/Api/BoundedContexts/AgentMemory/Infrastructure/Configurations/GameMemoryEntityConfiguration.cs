using Api.BoundedContexts.AgentMemory.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.BoundedContexts.AgentMemory.Infrastructure.Configurations;

/// <summary>
/// EF Core configuration for GameMemoryEntity.
/// Table schema, indexes, and JSONB columns for per-game memory persistence.
/// </summary>
internal sealed class GameMemoryEntityConfiguration : IEntityTypeConfiguration<GameMemoryEntity>
{
    public void Configure(EntityTypeBuilder<GameMemoryEntity> builder)
    {
        builder.ToTable("game_memories");

        builder.HasKey(e => e.Id);

        // --- Scalar Properties ---

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.GameId)
            .HasColumnName("game_id")
            .IsRequired();

        builder.Property(e => e.OwnerId)
            .HasColumnName("owner_id")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // --- JSON Columns ---

        builder.Property(e => e.HouseRulesJson)
            .HasColumnName("house_rules_json")
            .HasColumnType("jsonb");

        builder.Property(e => e.CustomSetupJson)
            .HasColumnName("custom_setup_json")
            .HasColumnType("jsonb");

        builder.Property(e => e.NotesJson)
            .HasColumnName("notes_json")
            .HasColumnType("jsonb");

        // --- Indexes ---

        builder.HasIndex(e => new { e.GameId, e.OwnerId })
            .IsUnique()
            .HasDatabaseName("ix_game_memories_game_id_owner_id");
    }
}
