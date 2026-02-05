using MediatR;

namespace Api.BoundedContexts.SessionTracking.Domain.Events;

/// <summary>
/// Event raised when a deck is shuffled.
/// </summary>
public record DeckShuffledEvent : INotification
{
    public Guid DeckId { get; init; }
    public Guid SessionId { get; init; }
    public string DeckName { get; init; } = string.Empty;
    public int CardsInDrawPile { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Event raised when cards are drawn.
/// </summary>
public record CardsDrawnEvent : INotification
{
    public Guid DeckId { get; init; }
    public Guid SessionId { get; init; }
    public Guid ParticipantId { get; init; }
    public string ParticipantName { get; init; } = string.Empty;
    public int CardCount { get; init; }
    /// <summary>
    /// Card IDs (only visible to the drawing participant).
    /// </summary>
    public Guid[] CardIds { get; init; } = [];
    public int RemainingCards { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Event raised when cards are discarded (visible to all).
/// </summary>
public record CardsDiscardedEvent : INotification
{
    public Guid DeckId { get; init; }
    public Guid SessionId { get; init; }
    public Guid ParticipantId { get; init; }
    public string ParticipantName { get; init; } = string.Empty;
    /// <summary>
    /// Discarded cards (visible to all).
    /// </summary>
    public CardInfo[] Cards { get; init; } = [];
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Event raised when cards are revealed (e.g., showing hand).
/// </summary>
public record CardsRevealedEvent : INotification
{
    public Guid DeckId { get; init; }
    public Guid SessionId { get; init; }
    public Guid ParticipantId { get; init; }
    public string ParticipantName { get; init; } = string.Empty;
    /// <summary>
    /// Revealed cards (now visible to all).
    /// </summary>
    public CardInfo[] Cards { get; init; } = [];
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Event raised when a deck is created.
/// </summary>
public record DeckCreatedEvent : INotification
{
    public Guid DeckId { get; init; }
    public Guid SessionId { get; init; }
    public string DeckName { get; init; } = string.Empty;
    public string DeckType { get; init; } = string.Empty;
    public int CardCount { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Event raised when a deck is reset.
/// </summary>
public record DeckResetEvent : INotification
{
    public Guid DeckId { get; init; }
    public Guid SessionId { get; init; }
    public string DeckName { get; init; } = string.Empty;
    public int CardCount { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Card information for SSE events.
/// </summary>
public record CardInfo
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public string? Suit { get; init; }
    public string? Value { get; init; }
}
