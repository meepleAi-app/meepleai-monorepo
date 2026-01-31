using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.GameManagement;

/// <summary>
/// Entity Framework configuration for GameStateSnapshot entity.
/// Issue #2403: GameSessionState Entity (snapshots for undo/history)
/// </summary>
internal sealed class GameStateSnapshotEntityConfiguration : IEntityTypeConfiguration<GameStateSnapshotEntity>
{
    public void Configure(EntityTypeBuilder<GameStateSnapshotEntity> builder)
    {
        builder.ToTable("game_state_snapshots");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(s => s.SessionStateId)
            .HasColumnName("session_state_id")
            .IsRequired();

        builder.Property(s => s.StateJson)
            .HasColumnName("state_json")
            .HasColumnType("jsonb") // PostgreSQL JSONB for efficient JSON storage
            .IsRequired();

        builder.Property(s => s.TurnNumber)
            .HasColumnName("turn_number")
            .IsRequired();

        builder.Property(s => s.Description)
            .HasColumnName("description")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(s => s.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(255)
            .IsRequired();

        // Indexes for query performance
        builder.HasIndex(s => s.SessionStateId)
            .HasDatabaseName("ix_game_state_snapshots_session_state_id");

        builder.HasIndex(s => new { s.SessionStateId, s.TurnNumber })
            .HasDatabaseName("ix_game_state_snapshots_session_state_id_turn_number")
            .IsUnique(); // Unique turn number per session state

        builder.HasIndex(s => s.CreatedAt)
            .HasDatabaseName("ix_game_state_snapshots_created_at");

        // Foreign key configured in GameSessionStateEntityConfiguration
    }
}
