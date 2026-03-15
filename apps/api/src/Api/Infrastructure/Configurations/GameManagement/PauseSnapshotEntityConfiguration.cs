using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.GameManagement;

/// <summary>
/// EF Core configuration for PauseSnapshotEntity.
/// Full-state snapshots stored on session pause; distinct from delta-based session_snapshots.
/// </summary>
internal sealed class PauseSnapshotEntityConfiguration : IEntityTypeConfiguration<PauseSnapshotEntity>
{
    public void Configure(EntityTypeBuilder<PauseSnapshotEntity> builder)
    {
        builder.ToTable("pause_snapshots");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(e => e.LiveGameSessionId)
            .HasColumnName("live_game_session_id")
            .IsRequired();

        builder.Property(e => e.CurrentTurn)
            .HasColumnName("current_turn")
            .IsRequired();

        builder.Property(e => e.CurrentPhase)
            .HasColumnName("current_phase")
            .HasMaxLength(100);

        // JSONB columns
        builder.Property(e => e.PlayerScoresJson)
            .HasColumnName("player_scores_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.AttachmentIdsJson)
            .HasColumnName("attachment_ids_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.DisputesJson)
            .HasColumnName("disputes_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(e => e.AgentConversationSummary)
            .HasColumnName("agent_conversation_summary")
            .HasMaxLength(5000);

        builder.Property(e => e.GameStateJson)
            .HasColumnName("game_state_json")
            .HasColumnType("jsonb");

        builder.Property(e => e.SavedAt)
            .HasColumnName("saved_at")
            .IsRequired();

        builder.Property(e => e.SavedByUserId)
            .HasColumnName("saved_by_user_id")
            .IsRequired();

        builder.Property(e => e.IsAutoSave)
            .HasColumnName("is_auto_save")
            .IsRequired();

        // Indexes
        builder.HasIndex(e => e.LiveGameSessionId)
            .HasDatabaseName("ix_pause_snapshots_live_game_session_id");

        builder.HasIndex(e => new { e.LiveGameSessionId, e.SavedAt })
            .HasDatabaseName("ix_pause_snapshots_session_saved_at");

        builder.HasIndex(e => new { e.LiveGameSessionId, e.IsAutoSave })
            .HasDatabaseName("ix_pause_snapshots_session_is_auto_save");
    }
}
