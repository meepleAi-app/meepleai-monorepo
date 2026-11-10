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

    /// <summary>
    /// Optional guest display name captured at RSVP time. Issue #1169.
    /// </summary>
    [Column("responded_by_name")]
    [MaxLength(120)]
    public string? RespondedByName { get; set; }

    [Required]
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [Required]
    [Column("created_by")]
    public Guid CreatedBy { get; set; }

    /// <summary>Issue #1928 Task B (DEC-B-8) — E2E test seeding scope marker.</summary>
    [Column("test_run_id")]
    [MaxLength(64)]
    public string? TestRunId { get; set; }

    /// <summary>
    /// Issue #1940 / iso-1 Fix 4: timestamp of the last RSVP confirmation email sent.
    /// Non-null = handler MUST skip resending on a retried event.
    /// </summary>
    [Column("rsvp_confirmation_sent_at")]
    public DateTimeOffset? RsvpConfirmationSentAt { get; set; }

    /// <summary>
    /// #4853: timestamp of the «the night is cancelled» email sent to this invitation.
    /// Non-null = the handler MUST NOT write again when the event is delivered a second time.
    /// </summary>
    [Column("cancellation_notice_sent_at")]
    public DateTimeOffset? CancellationNoticeSentAt { get; set; }

    /// <summary>
    /// #4846: the credential for «stop writing to this address». Not the RSVP <see cref="Token"/>: the organizer holds
    /// that one. Written in the emails to the address and nowhere else. Null only on a row older than the column.
    /// </summary>
    [Column("unsubscribe_key")]
    [MaxLength(32)]
    public string? UnsubscribeKey { get; set; }

    /// <summary>
    /// #4846: timestamp of the 24 hour reminder sent to this invitation.
    /// Non-null = the handler MUST NOT write again when the event is delivered a second time.
    /// </summary>
    [Column("reminder_24h_sent_at")]
    public DateTimeOffset? Reminder24hSentAt { get; set; }

    /// <summary>
    /// #4744: optimistic concurrency through PostgreSQL's <c>xmin</c> system column (ADR-060). Server-owned: it changes
    /// with every <c>UPDATE</c> of the row, so two writers that overlap cannot both win.
    /// </summary>
    public uint Xmin { get; set; }
}
