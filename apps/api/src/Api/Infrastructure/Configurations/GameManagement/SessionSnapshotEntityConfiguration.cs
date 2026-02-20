using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Configurations.GameManagement;

/// <summary>
/// EF Core configuration for SessionSnapshot persistence entity.
/// Issue #4755: SessionSnapshot - Delta-based History + State Reconstruction.
/// </summary>
internal sealed class SessionSnapshotEntityConfiguration : IEntityTypeConfiguration<SessionSnapshotEntity>
{
    public void Configure(EntityTypeBuilder<SessionSnapshotEntity> builder)
    {
        builder.ToTable("session_snapshots");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(s => s.SessionId)
            .HasColumnName("session_id")
            .IsRequired();

        builder.Property(s => s.SnapshotIndex)
            .HasColumnName("snapshot_index")
            .IsRequired();

        builder.Property(s => s.TriggerType)
            .HasColumnName("trigger_type")
            .IsRequired();

        builder.Property(s => s.TriggerDescription)
            .HasColumnName("trigger_description")
            .HasMaxLength(500);

        builder.Property(s => s.DeltaDataJson)
            .HasColumnName("delta_data_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(s => s.IsCheckpoint)
            .HasColumnName("is_checkpoint")
            .IsRequired();

        builder.Property(s => s.TurnIndex)
            .HasColumnName("turn_index")
            .IsRequired();

        builder.Property(s => s.PhaseIndex)
            .HasColumnName("phase_index");

        builder.Property(s => s.Timestamp)
            .HasColumnName("timestamp")
            .IsRequired();

        builder.Property(s => s.CreatedByPlayerId)
            .HasColumnName("created_by_player_id");

        // Indexes
        builder.HasIndex(s => s.SessionId)
            .HasDatabaseName("ix_session_snapshots_session_id");

        builder.HasIndex(s => new { s.SessionId, s.SnapshotIndex })
            .HasDatabaseName("ix_session_snapshots_session_id_snapshot_index")
            .IsUnique();

        builder.HasIndex(s => new { s.SessionId, s.IsCheckpoint })
            .HasDatabaseName("ix_session_snapshots_session_id_is_checkpoint");

        builder.HasIndex(s => s.Timestamp)
            .HasDatabaseName("ix_session_snapshots_timestamp");
    }
}
