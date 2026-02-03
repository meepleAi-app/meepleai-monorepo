using System.ComponentModel.DataAnnotations;

namespace Api.Infrastructure.Entities.SessionTracking;

/// <summary>
/// Persistence entity for DiceRoll (EF Core mapping).
/// Maps to session_tracking_dice_rolls table.
/// Phase 2 placeholder for GST-003.
/// </summary>
public class DiceRollEntity
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid ParticipantId { get; set; }

    [MaxLength(10)]
    public string DiceType { get; set; } = string.Empty;

    public int RollCount { get; set; } = 1;
    public string Results { get; set; } = string.Empty; // JSONB
    public DateTime Timestamp { get; set; }

    // Navigation properties
    public SessionEntity? Session { get; set; }
    public ParticipantEntity? Participant { get; set; }
}
