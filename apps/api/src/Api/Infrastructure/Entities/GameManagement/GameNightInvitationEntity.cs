using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities.GameManagement;

/// <summary>
/// EF Core persistence entity for token-based game-night invitations.
/// Issue #607 (Wave A.5a): GameNight token-based RSVP backend extension.
/// </summary>
/// <remarks>
/// Distinct from <see cref="GameNightRsvpEntity"/>: that table tracks RSVPs by
/// <c>UserId</c> for users already known to the system; this table persists the
/// public, token-addressable invitation lifecycle (sent by email to addresses
/// that may not yet be registered).
/// </remarks>
[Table("game_night_invitations")]
public class GameNightInvitationEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("token")]
    [MaxLength(32)]
    public string Token { get; set; } = string.Empty;

    [Required]
    [Column("game_night_id")]
    public Guid GameNightId { get; set; }

    [Required]
    [Column("email")]
    [MaxLength(320)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Column("status")]
    [MaxLength(20)]
    public string Status { get; set; } = "Pending";

    [Required]
    [Column("expires_at")]
    public DateTimeOffset ExpiresAt { get; set; }

    [Column("responded_at")]
    public DateTimeOffset? RespondedAt { get; set; }

    [Column("responded_by_user_id")]
    public Guid? RespondedByUserId { get; set; }

    [Required]
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [Required]
    [Column("created_by")]
    public Guid CreatedBy { get; set; }
}
