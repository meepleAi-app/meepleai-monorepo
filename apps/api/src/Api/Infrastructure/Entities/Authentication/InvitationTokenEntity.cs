using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities.Authentication;

/// <summary>
/// Infrastructure entity for admin-issued invitation tokens.
/// Maps to InvitationToken domain aggregate.
/// </summary>
[Table("invitation_tokens")]
public sealed class InvitationTokenEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(256)]
    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    [Column("role")]
    public string Role { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    [Column("token_hash")]
    public string TokenHash { get; set; } = string.Empty;

    [Required]
    [Column("invited_by_user_id")]
    public Guid InvitedByUserId { get; set; }

    [Required]
    [MaxLength(16)]
    [Column("status")]
    public string Status { get; set; } = "Pending";

    [Required]
    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Column("accepted_at")]
    public DateTime? AcceptedAt { get; set; }

    [Column("accepted_by_user_id")]
    public Guid? AcceptedByUserId { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("revoked_at")]
    public DateTime? RevokedAt { get; set; }

    // Admin Invitation Flow: custom message and pending user provisioning
    [MaxLength(500)]
    [Column("custom_message")]
    public string? CustomMessage { get; set; }

    [Column("pending_user_id")]
    public Guid? PendingUserId { get; set; }

    // Navigation properties
    [ForeignKey(nameof(InvitedByUserId))]
    public UserEntity? InvitedByUser { get; set; }

    [ForeignKey(nameof(AcceptedByUserId))]
    public UserEntity? AcceptedByUser { get; set; }

    [ForeignKey(nameof(PendingUserId))]
    public UserEntity? PendingUser { get; set; }

    public ICollection<InvitationGameSuggestionEntity> GameSuggestions { get; set; } = new List<InvitationGameSuggestionEntity>();
}
