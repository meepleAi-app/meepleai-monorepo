using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities.Authentication;

/// <summary>
/// Infrastructure entity for access requests.
/// Maps to AccessRequest domain aggregate.
/// </summary>
[Table("access_requests")]
public sealed class AccessRequestEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(256)]
    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    [Column("status")]
    public string Status { get; set; } = "Pending";

    [Required]
    [Column("requested_at")]
    public DateTime RequestedAt { get; set; }

    [Column("reviewed_at")]
    public DateTime? ReviewedAt { get; set; }

    [Column("reviewed_by")]
    public Guid? ReviewedBy { get; set; }

    [MaxLength(500)]
    [Column("rejection_reason")]
    public string? RejectionReason { get; set; }

    [Column("invitation_id")]
    public Guid? InvitationId { get; set; }

    // Navigation properties
    [ForeignKey(nameof(ReviewedBy))]
    public UserEntity? ReviewedByUser { get; set; }

    [ForeignKey(nameof(InvitationId))]
    public InvitationTokenEntity? Invitation { get; set; }
}
