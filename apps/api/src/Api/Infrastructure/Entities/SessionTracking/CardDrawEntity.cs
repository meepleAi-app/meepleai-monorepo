using System.ComponentModel.DataAnnotations;

namespace Api.Infrastructure.Entities.SessionTracking;

/// <summary>
/// Persistence entity for CardDraw (EF Core mapping).
/// Maps to session_tracking_card_draws table.
/// Phase 2 placeholder for GST-003.
/// </summary>
public class CardDrawEntity
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid ParticipantId { get; set; }

    [MaxLength(20)]
    public string DeckType { get; set; } = string.Empty;

    public Guid? DeckId { get; set; }

    [MaxLength(50)]
    public string CardValue { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; }

    // Navigation properties
    public SessionEntity? Session { get; set; }
    public ParticipantEntity? Participant { get; set; }
}
