using System.ComponentModel.DataAnnotations;

namespace Api.BoundedContexts.SessionTracking.Domain.Entities;

/// <summary>
/// Card draw entity representing a card draw in a session.
/// Phase 2 implementation - placeholder for GST-003.
/// </summary>
public class CardDraw
{
    /// <summary>
    /// Card draw unique identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Session reference.
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// Participant who drew the card.
    /// </summary>
    public Guid ParticipantId { get; set; }

    /// <summary>
    /// Type of deck (Standard52, GameSpecific, Custom).
    /// </summary>
    [MaxLength(20)]
    public string DeckType { get; private set; } = string.Empty;

    /// <summary>
    /// Optional reference to custom deck.
    /// </summary>
    public Guid? DeckId { get; set; }

    /// <summary>
    /// Card value (e.g., "Ace of Spades", "Event Card #3").
    /// </summary>
    [MaxLength(50)]
    public string CardValue { get; private set; } = string.Empty;

    /// <summary>
    /// When the card was drawn.
    /// </summary>
    public DateTime Timestamp { get; private set; } = DateTime.UtcNow;

    /// <summary>
    /// Public constructor for EF Core (Phase 2 placeholder).
    /// </summary>
    public CardDraw()
    {
    }

    // Factory method and business logic will be implemented in Phase 2 (GST-003)
}