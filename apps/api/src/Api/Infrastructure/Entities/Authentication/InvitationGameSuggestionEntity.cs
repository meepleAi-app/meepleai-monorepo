using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities.Authentication;

/// <summary>
/// Infrastructure entity for game suggestions attached to invitation tokens.
/// Admin Invitation Flow: stores pre-added or suggested games for invitees.
/// </summary>
[Table("invitation_game_suggestions")]
public sealed class InvitationGameSuggestionEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("invitation_token_id")]
    public Guid InvitationTokenId { get; set; }

    [Required]
    [Column("game_id")]
    public Guid GameId { get; set; }

    [Required]
    [MaxLength(16)]
    [Column("type")]
    public string Type { get; set; } = "Suggested";

    // Navigation properties
    [ForeignKey(nameof(InvitationTokenId))]
    public InvitationTokenEntity? InvitationToken { get; set; }
}
