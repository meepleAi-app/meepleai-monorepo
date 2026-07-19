using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.GameManagement;

/// <summary>
/// EF Core configuration for GameNightSessionEntity.
/// Links game night events to play sessions with ordering and outcome tracking.
/// </summary>
internal class GameNightSessionEntityConfiguration : IEntityTypeConfiguration<GameNightSessionEntity>
{
    public void Configure(EntityTypeBuilder<GameNightSessionEntity> builder)
    {
        builder.ToTable("game_night_sessions");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(s => s.GameNightEventId).HasColumnName("game_night_event_id").IsRequired();
        builder.Property(s => s.SessionId).HasColumnName("session_id").IsRequired();
        builder.Property(s => s.GameId).HasColumnName("game_id").IsRequired();
        builder.Property(s => s.GameTitle).HasColumnName("game_title").HasMaxLength(200).IsRequired();
        builder.Property(s => s.PlayOrder).HasColumnName("play_order").IsRequired();
        builder.Property(s => s.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        builder.Property(s => s.WinnerId).HasColumnName("winner_id");
        builder.Property(s => s.StartedAt).HasColumnName("started_at");
        builder.Property(s => s.CompletedAt).HasColumnName("completed_at");

        builder.HasIndex(s => new { s.GameNightEventId, s.PlayOrder })
            .HasDatabaseName("IX_game_night_sessions_event_play_order")
            .IsUnique();

        builder.HasIndex(s => s.SessionId)
            .HasDatabaseName("IX_game_night_sessions_session_id");

        builder.HasIndex(s => s.GameId)
            .HasDatabaseName("IX_game_night_sessions_game_id");

        // Issue #3157 C1 — restore the "max 1 live session per GameNight" invariant as a
        // DB constraint. The original raw-SQL partial unique index (T1 #365) was silently
        // dropped by the #2875 migration flatten (ef migrations add Initial only reflects the
        // model). Declaring it on the MODEL here makes it EF-tracked, so it survives future
        // flattens — fixing the root cause, not just restoring the symptom.
        builder.HasIndex(s => s.GameNightEventId)
            .HasDatabaseName("ix_game_night_sessions_unique_active")
            .IsUnique()
            .HasFilter("status = 'InProgress'");
    }
}
