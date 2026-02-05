using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Command to draw cards from a deck.
/// </summary>
public record DrawCardsCommand : IRequest<DrawCardsResult>
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
    /// Participant ID drawing the cards.
    /// </summary>
    public Guid ParticipantId { get; init; }

    /// <summary>
    /// Number of cards to draw.
    /// </summary>
    public int Count { get; init; } = 1;
}

/// <summary>
/// Result of drawing cards.
/// </summary>
public record DrawCardsResult
{
    public Guid DeckId { get; init; }
    public Guid ParticipantId { get; init; }
    public List<CardDto> Cards { get; init; } = [];
    public int RemainingCards { get; init; }
}

/// <summary>
/// DTO for card information.
/// </summary>
public record CardDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public string? Suit { get; init; }
    public string? Value { get; init; }
}
