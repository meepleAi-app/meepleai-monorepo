using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.GameManagement;

/// <summary>
/// EF Core configuration for LiveRoundScoreEntity.
/// Issue #4750: Table schema and indexes for per-round scoring.
/// </summary>
internal sealed class LiveRoundScoreEntityConfiguration : IEntityTypeConfiguration<LiveRoundScoreEntity>
{
    public void Configure(EntityTypeBuilder<LiveRoundScoreEntity> builder)
    {
        builder.ToTable("live_session_round_scores");

        builder.HasKey(e => e.Id);

        // --- Scalar Properties ---

        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.LiveGameSessionId)
            .HasColumnName("live_game_session_id")
            .IsRequired();

        builder.Property(e => e.PlayerId)
            .HasColumnName("player_id")
            .IsRequired();

        builder.Property(e => e.Round)
            .HasColumnName("round")
            .IsRequired();

        builder.Property(e => e.Dimension)
            .HasColumnName("dimension")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Value)
            .HasColumnName("value")
            .IsRequired();

        builder.Property(e => e.Unit)
            .HasColumnName("unit")
            .HasMaxLength(20);

        builder.Property(e => e.RecordedAt)
            .HasColumnName("recorded_at")
            .IsRequired();

        // --- Indexes ---

        builder.HasIndex(e => new { e.LiveGameSessionId, e.PlayerId, e.Round, e.Dimension })
            .HasDatabaseName("ix_live_round_scores_session_player_round_dim")
            .IsUnique();

        builder.HasIndex(e => e.LiveGameSessionId)
            .HasDatabaseName("ix_live_round_scores_session_id");

        // --- Relationships ---
        // LiveGameSession → cascade configured in LiveGameSessionEntityConfiguration

        builder.HasOne(e => e.Player)
            .WithMany()
            .HasForeignKey(e => e.PlayerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
