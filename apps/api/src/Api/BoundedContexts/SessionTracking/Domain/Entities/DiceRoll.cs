using System.ComponentModel.DataAnnotations;

namespace Api.BoundedContexts.SessionTracking.Domain.Entities;

/// <summary>
/// Dice roll entity representing a dice roll in a session.
/// Phase 2 implementation - placeholder for GST-003.
/// </summary>
public class DiceRoll
{
    /// <summary>
    /// Dice roll unique identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Session reference.
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// Participant who performed the roll.
    /// </summary>
    public Guid ParticipantId { get; set; }

    /// <summary>
    /// Type of dice rolled (d4, d6, d8, d10, d12, d20, d100, custom).
    /// </summary>
    [MaxLength(10)]
    public string DiceType { get; private set; } = string.Empty;

    /// <summary>
    /// Number of dice rolled.
    /// </summary>
    public int RollCount { get; private set; } = 1;

    /// <summary>
    /// Roll results stored as JSONB.
    /// Example: {"rolls": [4, 2, 6], "total": 12}
    /// </summary>
    public string Results { get; private set; } = string.Empty;

    /// <summary>
    /// When the roll occurred.
    /// </summary>
    public DateTime Timestamp { get; private set; } = DateTime.UtcNow;

    /// <summary>
    /// Public constructor for EF Core (Phase 2 placeholder).
    /// </summary>
    public DiceRoll()
    {
    }

    // Factory method and business logic will be implemented in Phase 2 (GST-003)
}