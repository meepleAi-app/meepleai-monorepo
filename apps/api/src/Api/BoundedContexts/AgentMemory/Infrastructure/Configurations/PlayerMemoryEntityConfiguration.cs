using Api.BoundedContexts.AgentMemory.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.BoundedContexts.AgentMemory.Infrastructure.Configurations;

/// <summary>
/// EF Core configuration for PlayerMemoryEntity.
/// Table schema, indexes, and JSONB columns for player game statistics persistence.
/// </summary>
internal sealed class PlayerMemoryEntityConfiguration : IEntityTypeConfiguration<PlayerMemoryEntity>
{
    public void Configure(EntityTypeBuilder<PlayerMemoryEntity> builder)
    {
        builder.ToTable("player_memories");

        builder.HasKey(e => e.Id);

        // --- Scalar Properties ---

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.UserId)
            .HasColumnName("user_id");

        builder.Property(e => e.GuestName)
            .HasColumnName("guest_name")
            .HasMaxLength(200);

        builder.Property(e => e.GroupId)
            .HasColumnName("group_id");

        builder.Property(e => e.ClaimedAt)
            .HasColumnName("claimed_at");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // --- JSON Columns ---

        builder.Property(e => e.GameStatsJson)
            .HasColumnName("game_stats_json")
            .HasColumnType("jsonb");

        // --- Indexes ---

        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("ix_player_memories_user_id")
            .HasFilter("user_id IS NOT NULL");

        builder.HasIndex(e => e.GroupId)
            .HasDatabaseName("ix_player_memories_group_id")
            .HasFilter("group_id IS NOT NULL");

        builder.HasIndex(e => e.GuestName)
            .HasDatabaseName("ix_player_memories_guest_name")
            .HasFilter("user_id IS NULL");
    }
}
