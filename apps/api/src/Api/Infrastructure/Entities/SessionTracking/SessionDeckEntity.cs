using Api.BoundedContexts.SessionTracking.Domain.Entities;

namespace Api.Infrastructure.Entities.SessionTracking;

/// <summary>
/// Persistence entity for session decks.
/// </summary>
public class SessionDeckEntity
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DeckType DeckType { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastShuffledAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// JSON array of card IDs in draw pile order.
    /// </summary>
    public string DrawPileJson { get; set; } = "[]";

    /// <summary>
    /// JSON array of card IDs in discard pile order.
    /// </summary>
    public string DiscardPileJson { get; set; } = "[]";

    /// <summary>
    /// JSON object mapping participant IDs to arrays of card IDs.
    /// </summary>
    public string HandsJson { get; set; } = "{}";

    // Navigation properties
    public SessionEntity? Session { get; set; }
    public ICollection<CardEntity> Cards { get; set; } = [];
}

/// <summary>
/// Persistence entity for cards.
/// </summary>
public class CardEntity
{
    public Guid Id { get; set; }
    public Guid SessionDeckId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? Suit { get; set; }
    public string? Value { get; set; }
    public int SortOrder { get; set; }

    // Navigation property
    public SessionDeckEntity? SessionDeck { get; set; }
}
