using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities.GameManagement;

/// <summary>
/// EF Core persistence entity for game night sessions.
/// Links a GameNightEvent to a Session with play order and outcome tracking.
/// </summary>
[Table("game_night_sessions")]
public class GameNightSessionEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("game_night_event_id")]
    public Guid GameNightEventId { get; set; }

    [Required]
    [Column("session_id")]
    public Guid SessionId { get; set; }

    [Required]
    [Column("game_id")]
    public Guid GameId { get; set; }

    [Required]
    [MaxLength(200)]
    [Column("game_title")]
    public string GameTitle { get; set; } = string.Empty;

    [Required]
    [Column("play_order")]
    public int PlayOrder { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("status")]
    public string Status { get; set; } = "Pending";

    [Column("winner_id")]
    public Guid? WinnerId { get; set; }

    [Column("started_at")]
    public DateTimeOffset? StartedAt { get; set; }

    [Column("completed_at")]
    public DateTimeOffset? CompletedAt { get; set; }

    // Navigation
    public GameNightEventEntity GameNightEvent { get; set; } = null!;
}
