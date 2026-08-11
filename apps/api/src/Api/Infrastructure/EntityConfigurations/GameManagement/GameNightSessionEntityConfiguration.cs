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
    /// <summary>
    /// Name of the partial-unique index enforcing "max 1 live (InProgress) session per GameNight"
    /// (invariante #10, Issue #3157 C1). Single source of truth: referenced by the index declaration
    /// below AND by every Postgres 23505 unique-violation catch that maps this constraint to HTTP 409
    /// (create-path <c>CreateSessionCommandHandler</c> + go-live-path <c>GoLiveSessionCommandHandler</c>).
    /// Keeping the literal in one place prevents the catch from silently drifting away from the index
    /// name and turning a real 409 into an uncaught 500.
    /// </summary>
    public const string UniqueActiveIndexName = "ix_game_night_sessions_unique_active";

    /// <summary>
    /// Name of the per-night play-order unique index on <c>(GameNightEventId, PlayOrder)</c>.
    /// Single source of truth: referenced by the index declaration below AND by the Postgres 23505
    /// unique-violation catch in <c>CreateSessionCommandHandler</c> that maps a concurrent same-night
    /// draft PlayOrder collision (invariante #19 — drafts coexist) to a retryable HTTP 409 rather than
    /// letting it surface as a raw 500 (epic #3188 post-review, project rule #2568). Keeping the literal
    /// in one place prevents the catch from drifting away from the index name.
    /// </summary>
    public const string PlayOrderIndexName = "IX_game_night_sessions_event_play_order";

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
            .HasDatabaseName(PlayOrderIndexName)
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
            .HasDatabaseName(UniqueActiveIndexName)
            .IsUnique()
            .HasFilter("status = 'InProgress'");
    }
}
