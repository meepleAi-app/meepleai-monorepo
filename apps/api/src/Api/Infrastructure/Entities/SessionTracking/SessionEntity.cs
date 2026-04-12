using System.ComponentModel.DataAnnotations;

namespace Api.Infrastructure.Entities.SessionTracking;

/// <summary>
/// Persistence entity for Session (EF Core mapping).
/// Maps to session_tracking_sessions table.
/// </summary>
public class SessionEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? GameId { get; set; }

    [MaxLength(6)]
    public string SessionCode { get; set; } = string.Empty;

    [MaxLength(20)]
    public string SessionType { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Status { get; set; } = string.Empty;

    public DateTime SessionDate { get; set; }

    [MaxLength(100)]
    public string? Location { get; set; }

    public DateTime? FinalizedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    [Timestamp]
    public byte[]? RowVersion { get; set; }

    /// <summary>
    /// Invite token for session sharing (Issue #3354).
    /// </summary>
    [MaxLength(64)]
    public string? InviteToken { get; set; }

    /// <summary>
    /// When the invite token expires (null = never expires).
    /// </summary>
    public DateTime? InviteExpiresAt { get; set; }

    /// <summary>
    /// Turn order as JSON array of participant IDs (Session Flow v2.1).
    /// </summary>
    public string? TurnOrderJson { get; set; }

    /// <summary>
    /// Method used to set turn order: "Manual" | "Random".
    /// </summary>
    [MaxLength(16)]
    public string? TurnOrderMethod { get; set; }

    /// <summary>
    /// Seed used when TurnOrderMethod=Random, for audit/reproducibility.
    /// </summary>
    public int? TurnOrderSeed { get; set; }

    /// <summary>
    /// Zero-based index of current player in turn order.
    /// </summary>
    public int? CurrentTurnIndex { get; set; }

    // Navigation properties
    public ICollection<ParticipantEntity> Participants { get; set; } = new List<ParticipantEntity>();
}
