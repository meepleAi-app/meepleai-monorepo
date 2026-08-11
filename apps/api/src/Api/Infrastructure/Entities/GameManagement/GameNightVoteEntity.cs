using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities.GameManagement;

/// <summary>
/// EF Core persistence entity for a candidate-game approval vote on a game night.
/// Issue #2700: GameNight candidate voting.
/// </summary>
[Table("game_night_votes")]
public class GameNightVoteEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("event_id")]
    public Guid EventId { get; set; }

    [Required]
    [Column("voter_user_id")]
    public Guid VoterUserId { get; set; }

    [Required]
    [Column("candidate_game_id")]
    public Guid CandidateGameId { get; set; }

    [Required]
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    public GameNightEventEntity Event { get; set; } = null!;
}
