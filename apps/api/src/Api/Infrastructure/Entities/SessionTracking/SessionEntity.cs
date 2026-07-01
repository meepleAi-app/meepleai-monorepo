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

    /// <summary>#2632 SI-1: optional link to the libro-game campaign (GameNight-attached play).</summary>
    public Guid? GamebookCampaignId { get; set; }

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

    /// <summary>
    /// When the session transitioned to live mode (via Session.OpenLiveMode).
    /// Null until live mode is opened. Asse A semantic alignment #1896 (T2,
    /// invariante #11) — column <c>started_at</c>.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Polymorphic scoring type for the session — one of <see cref="Api.BoundedContexts.SessionTracking.Domain.Enums.ScoreType"/>
    /// values stored as string ("Points", "BinaryWin", "Objectives", "Ranking").
    /// Asse A semantic alignment #1896 (T9, DEC-1) — column <c>scoring_type</c>.
    /// Default = "Points".
    /// </summary>
    [MaxLength(20)]
    public string ScoringType { get; set; } = "Points";

    /// <summary>
    /// Polymorphic score data as JSONB. Shape varies by <see cref="ScoringType"/>.
    /// Asse A semantic alignment #1896 (T9, DEC-1) — column <c>score_data</c>.
    /// Default = "{}".
    /// </summary>
    public string ScoreData { get; set; } = "{}";

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
