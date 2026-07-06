using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities.GameManagement;

/// <summary>
/// EF Core persistence entity for game night events.
/// Issue #42: GameNightEvent + GameNightRsvp domain entities.
/// </summary>
[Table("game_night_events")]
public class GameNightEventEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("organizer_id")]
    public Guid OrganizerId { get; set; }

    [Required]
    [MaxLength(200)]
    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("description")]
    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    [Column("scheduled_at")]
    public DateTimeOffset ScheduledAt { get; set; }

    [Column("location")]
    [MaxLength(500)]
    public string? Location { get; set; }

    [Column("max_players")]
    public int? MaxPlayers { get; set; }

    [Column("game_ids")]
    public string GameIdsJson { get; set; } = "[]";

    [Required]
    [Column("status")]
    [MaxLength(20)]
    public string Status { get; set; } = "Draft";

    [Column("reminder_24h_sent_at")]
    public DateTimeOffset? Reminder24hSentAt { get; set; }

    [Column("reminder_1h_sent_at")]
    public DateTimeOffset? Reminder1hSentAt { get; set; }

    // RSVP deadline — ADR-074 (#2383 follow-up)
    [Column("rsvp_deadline")]
    public DateTimeOffset? RsvpDeadline { get; set; }

    [Column("rsvp_closed_at")]
    public DateTimeOffset? RsvpClosedAt { get; set; }

    [Required]
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>
    /// Issue #1928 Task B (DEC-B-8) — E2E test seeding scope marker.
    /// Null for production entities; non-null only when seeded via Testing BC.
    /// CleanupTestEntitiesCommand uses ExecuteDeleteAsync WHERE TestRunId = @testRunId.
    /// </summary>
    [Column("test_run_id")]
    [MaxLength(64)]
    public string? TestRunId { get; set; }

    public List<GameNightRsvpEntity> Rsvps { get; set; } = [];

    public List<GameNightSessionEntity> Sessions { get; set; } = [];

    // Candidate voting (approval model) — Issue #2700
    [Column("voting_winner_game_id")]
    public Guid? VotingWinnerGameId { get; set; }

    public List<GameNightVoteEntity> Votes { get; set; } = [];

    // Summary share-token + archive — Issue #2702
    [Column("share_token")]
    [MaxLength(50)]
    public string? ShareToken { get; set; }

    [Column("is_shared")]
    public bool IsShared { get; set; }

    [Column("is_archived")]
    public bool IsArchived { get; set; }

    // Optimistic concurrency via PostgreSQL's xmin system column (Issue #2703, ADR-060).
    // Server-owned: Postgres assigns xmin = transaction-id-of-last-write per row.
    // The repository round-trips this value so the detached Update emits a
    // WHERE id = @id AND xmin = @original concurrency check.
    public uint Xmin { get; set; }
}
