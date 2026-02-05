using Api.BoundedContexts.SessionTracking.Application.Commands;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

/// <summary>
/// Query to get all decks in a session.
/// </summary>
public record GetSessionDecksQuery : IRequest<List<SessionDeckDto>>
{
    /// <summary>
    /// Session ID.
    /// </summary>
    public Guid SessionId { get; init; }
}

/// <summary>
/// DTO for session deck information.
/// </summary>
public record SessionDeckDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string DeckType { get; init; } = string.Empty;
    public int TotalCards { get; init; }
    public int CardsInDrawPile { get; init; }
    public int CardsInDiscardPile { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? LastShuffledAt { get; init; }
}

/// <summary>
/// Query to get a participant's hand.
/// </summary>
public record GetPlayerHandQuery : IRequest<PlayerHandDto>
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
    /// Participant ID.
    /// </summary>
    public Guid ParticipantId { get; init; }
}

/// <summary>
/// DTO for a player's hand.
/// </summary>
public record PlayerHandDto
{
    public Guid DeckId { get; init; }
    public Guid ParticipantId { get; init; }
    public List<CardDto> Cards { get; init; } = [];
}

/// <summary>
/// Query to get the discard pile.
/// </summary>
public record GetDiscardPileQuery : IRequest<DiscardPileDto>
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
    /// Maximum number of cards to return (from top).
    /// </summary>
    public int Limit { get; init; } = 10;
}

/// <summary>
/// DTO for the discard pile.
/// </summary>
public record DiscardPileDto
{
    public Guid DeckId { get; init; }
    public List<CardDto> Cards { get; init; } = [];
    public int TotalCount { get; init; }
}
