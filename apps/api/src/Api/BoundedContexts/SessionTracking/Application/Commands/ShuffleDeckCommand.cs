using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Command to shuffle a deck.
/// </summary>
public record ShuffleDeckCommand : IRequest<ShuffleDeckResult>
{
    /// <summary>
    /// Deck ID.
    /// </summary>
    public Guid DeckId { get; init; }

    /// <summary>
    /// Session ID.
    /// </summary>
    public Guid SessionId { get; init; }

    /// <summary>
    /// Whether to include discard pile in the shuffle.
    /// </summary>
    public bool IncludeDiscard { get; init; }
}

/// <summary>
/// Result of shuffling a deck.
/// </summary>
public record ShuffleDeckResult
{
    public Guid DeckId { get; init; }
    public int CardsInDrawPile { get; init; }
    public DateTime ShuffledAt { get; init; }
}
