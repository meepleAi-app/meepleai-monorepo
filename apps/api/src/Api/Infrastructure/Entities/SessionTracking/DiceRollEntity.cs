using System.ComponentModel.DataAnnotations;

namespace Api.Infrastructure.Entities.SessionTracking;

/// <summary>
/// Persistence entity for DiceRoll (EF Core mapping).
/// Maps to session_tracking_dice_rolls table.
/// Supports dice formulas like "2d6+3", "1d20-2".
/// </summary>
public class DiceRollEntity
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid ParticipantId { get; set; }

    /// <summary>
    /// Original formula used (e.g., "2D6+3", "1D20-2").
    /// </summary>
    [MaxLength(50)]
    public string Formula { get; set; } = string.Empty;

    /// <summary>
    /// Optional label for the roll (e.g., "Attack roll", "Damage").
    /// </summary>
    [MaxLength(100)]
    public string? Label { get; set; }

    /// <summary>
    /// Individual dice results (JSON array).
    /// Example: [4, 2, 6]
    /// </summary>
    public string Rolls { get; set; } = "[]"; // JSONB

    /// <summary>
    /// Modifier applied to the roll.
    /// </summary>
    public int Modifier { get; set; }

    /// <summary>
    /// Total result (sum of rolls + modifier).
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// When the roll occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Soft delete flag.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Soft delete timestamp.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    public SessionEntity? Session { get; set; }
    public ParticipantEntity? Participant { get; set; }
}
