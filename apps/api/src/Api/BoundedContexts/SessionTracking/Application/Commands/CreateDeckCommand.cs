using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Command to create a new deck in a session.
/// </summary>
public record CreateDeckCommand : IRequest<CreateDeckResult>
{
    /// <summary>
    /// Session ID.
    /// </summary>
    public Guid SessionId { get; init; }

    /// <summary>
    /// Deck name.
    /// </summary>
    public string Name { get; init; } = "Standard Deck";

    /// <summary>
    /// Type of deck: "standard" or "custom".
    /// </summary>
    public string DeckType { get; init; } = "standard";

    /// <summary>
    /// Whether to include jokers (for standard deck).
    /// </summary>
    public bool IncludeJokers { get; init; }

    /// <summary>
    /// Custom cards (for custom deck only).
    /// </summary>
    public List<CustomCardInput>? CustomCards { get; init; }
}

/// <summary>
/// Input for a custom card.
/// </summary>
public record CustomCardInput
{
    public string Name { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public string? Suit { get; init; }
    public string? Value { get; init; }
}

/// <summary>
/// Result of creating a deck.
/// </summary>
public record CreateDeckResult
{
    public Guid DeckId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string DeckType { get; init; } = string.Empty;
    public int CardCount { get; init; }
    public DateTime CreatedAt { get; init; }
}
