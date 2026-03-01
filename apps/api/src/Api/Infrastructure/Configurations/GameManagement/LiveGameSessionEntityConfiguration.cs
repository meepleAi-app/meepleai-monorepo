using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.GameManagement;

/// <summary>
/// EF Core configuration for LiveGameSessionEntity.
/// Issue #4750: Table schema, indexes, and relationships for live game sessions.
/// </summary>
internal sealed class LiveGameSessionEntityConfiguration : IEntityTypeConfiguration<LiveGameSessionEntity>
{
    public void Configure(EntityTypeBuilder<LiveGameSessionEntity> builder)
    {
        builder.ToTable("live_game_sessions");

        builder.HasKey(e => e.Id);

        // --- Scalar Properties ---

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.SessionCode)
            .HasColumnName("session_code")
            .HasMaxLength(6)
            .IsRequired();

        builder.Property(e => e.GameId)
            .HasColumnName("game_id");

        builder.Property(e => e.GameName)
            .HasColumnName("game_name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(e => e.ToolkitId)
            .HasColumnName("toolkit_id");

        builder.Property(e => e.CreatedByUserId)
            .HasColumnName("created_by_user_id")
            .IsRequired();

        builder.Property(e => e.Visibility)
            .HasColumnName("visibility")
            .IsRequired();

        builder.Property(e => e.GroupId)
            .HasColumnName("group_id");

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .IsRequired();

        builder.Property(e => e.CurrentTurnIndex)
            .HasColumnName("current_turn_index")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.StartedAt)
            .HasColumnName("started_at");

        builder.Property(e => e.PausedAt)
            .HasColumnName("paused_at");

        builder.Property(e => e.CompletedAt)
            .HasColumnName("completed_at");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(e => e.LastSavedAt)
            .HasColumnName("last_saved_at");

        builder.Property(e => e.Notes)
            .HasColumnName("notes")
            .HasMaxLength(2000);

        builder.Property(e => e.AgentMode)
            .HasColumnName("agent_mode")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.ChatSessionId)
            .HasColumnName("chat_session_id");

        // --- JSON Columns ---

        builder.Property(e => e.ScoringConfigJson)
            .HasColumnName("scoring_config_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.GameStateJson)
            .HasColumnName("game_state_json")
            .HasColumnType("jsonb");

        builder.Property(e => e.TurnOrderJson)
            .HasColumnName("turn_order_json")
            .HasColumnType("jsonb");

        // --- Concurrency Token ---

        builder.Property(e => e.RowVersion)
            .HasColumnName("row_version")
            .IsRowVersion();

        // --- Indexes ---

        builder.HasIndex(e => e.SessionCode)
            .HasDatabaseName("ix_live_game_sessions_session_code")
            .IsUnique();

        builder.HasIndex(e => e.CreatedByUserId)
            .HasDatabaseName("ix_live_game_sessions_created_by_user_id");

        builder.HasIndex(e => new { e.CreatedByUserId, e.Status })
            .HasDatabaseName("ix_live_game_sessions_user_status");

        builder.HasIndex(e => new { e.GameId, e.Status })
            .HasDatabaseName("ix_live_game_sessions_game_status")
            .HasFilter("game_id IS NOT NULL");

        builder.HasIndex(e => e.Status)
            .HasDatabaseName("ix_live_game_sessions_status");

        builder.HasIndex(e => e.CreatedAt)
            .HasDatabaseName("ix_live_game_sessions_created_at")
            .IsDescending(true);

        // --- Relationships ---

        builder.HasOne(e => e.Game)
            .WithMany()
            .HasForeignKey(e => e.GameId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Players)
            .WithOne(p => p.LiveGameSession)
            .HasForeignKey(p => p.LiveGameSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Teams)
            .WithOne(t => t.LiveGameSession)
            .HasForeignKey(t => t.LiveGameSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.RoundScores)
            .WithOne(s => s.LiveGameSession)
            .HasForeignKey(s => s.LiveGameSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.TurnRecords)
            .WithOne(t => t.LiveGameSession)
            .HasForeignKey(t => t.LiveGameSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
