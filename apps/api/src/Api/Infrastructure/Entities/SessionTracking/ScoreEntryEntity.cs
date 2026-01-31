using System.ComponentModel.DataAnnotations;

namespace Api.Infrastructure.Entities.SessionTracking;

/// <summary>
/// Persistence entity for ScoreEntry (EF Core mapping).
/// Maps to session_tracking_score_entries table.
/// </summary>
public class ScoreEntryEntity
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid ParticipantId { get; set; }
    public int? RoundNumber { get; set; }

    [MaxLength(50)]
    public string? Category { get; set; }

    public decimal ScoreValue { get; set; }
    public DateTime Timestamp { get; set; }
    public Guid CreatedBy { get; set; }

    // Navigation properties
    public SessionEntity? Session { get; set; }
    public ParticipantEntity? Participant { get; set; }
}
