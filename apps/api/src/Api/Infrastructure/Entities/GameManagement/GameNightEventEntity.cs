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

    [Required]
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    public List<GameNightRsvpEntity> Rsvps { get; set; } = [];
}
