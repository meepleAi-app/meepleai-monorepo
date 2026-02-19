using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.GameManagement;

/// <summary>
/// EF Core configuration for LiveTurnRecordEntity.
/// Issue #4750: Table schema and indexes for turn history.
/// </summary>
internal sealed class LiveTurnRecordEntityConfiguration : IEntityTypeConfiguration<LiveTurnRecordEntity>
{
    public void Configure(EntityTypeBuilder<LiveTurnRecordEntity> builder)
    {
        builder.ToTable("live_session_turn_records");

        builder.HasKey(e => e.Id);

        // --- Scalar Properties ---

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.LiveGameSessionId)
            .HasColumnName("live_game_session_id")
            .IsRequired();

        builder.Property(e => e.TurnIndex)
            .HasColumnName("turn_index")
            .IsRequired();

        builder.Property(e => e.PlayerId)
            .HasColumnName("player_id")
            .IsRequired();

        builder.Property(e => e.PhaseIndex)
            .HasColumnName("phase_index");

        builder.Property(e => e.PhaseName)
            .HasColumnName("phase_name")
            .HasMaxLength(100);

        builder.Property(e => e.StartedAt)
            .HasColumnName("started_at")
            .IsRequired();

        builder.Property(e => e.EndedAt)
            .HasColumnName("ended_at");

        // --- Indexes ---

        builder.HasIndex(e => new { e.LiveGameSessionId, e.TurnIndex })
            .HasDatabaseName("ix_live_turn_records_session_turn");

        builder.HasIndex(e => e.LiveGameSessionId)
            .HasDatabaseName("ix_live_turn_records_session_id");

        // --- Relationships ---
        // LiveGameSession → cascade configured in LiveGameSessionEntityConfiguration

        builder.HasOne(e => e.Player)
            .WithMany()
            .HasForeignKey(e => e.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
