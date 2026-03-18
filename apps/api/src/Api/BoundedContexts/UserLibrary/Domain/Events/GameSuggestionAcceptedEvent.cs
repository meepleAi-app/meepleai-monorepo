using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.UserLibrary.Domain.Events;

/// <summary>
/// Domain event raised when a user accepts a game suggestion.
/// The application handler dispatches AddGameToLibraryCommand in response.
/// </summary>
internal sealed class GameSuggestionAcceptedEvent : DomainEventBase
{
    /// <summary>
    /// The ID of the suggestion that was accepted.
    /// </summary>
    public Guid SuggestionId { get; }

    /// <summary>
    /// The ID of the user who accepted the suggestion.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// The ID of the game that was suggested.
    /// </summary>
    public Guid GameId { get; }

    public GameSuggestionAcceptedEvent(Guid suggestionId, Guid userId, Guid gameId)
    {
        SuggestionId = suggestionId;
        UserId = userId;
        GameId = gameId;
    }
}
