using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities.GameManagement;

/// <summary>
/// EF Core persistence entity for game night RSVPs.
/// Issue #42: GameNightEvent + GameNightRsvp domain entities.
/// </summary>
[Table("game_night_rsvps")]
public class GameNightRsvpEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("event_id")]
    public Guid EventId { get; set; }

    [Required]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [Required]
    [Column("status")]
    [MaxLength(20)]
    public string Status { get; set; } = "Pending";

    [Column("responded_at")]
    public DateTimeOffset? RespondedAt { get; set; }

    [Required]
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Issue #1928 Task B (DEC-B-8) — E2E test seeding scope marker.</summary>
    [Column("test_run_id")]
    [MaxLength(64)]
    public string? TestRunId { get; set; }

    public GameNightEventEntity Event { get; set; } = null!;
}
