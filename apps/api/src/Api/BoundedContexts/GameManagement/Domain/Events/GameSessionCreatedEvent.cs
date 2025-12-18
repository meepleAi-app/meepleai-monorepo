using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a new game session is created.
/// </summary>
internal sealed class GameSessionCreatedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the created game session.
    /// </summary>
    public Guid SessionId { get; }

    /// <summary>
    /// Gets the ID of the game being played.
    /// </summary>
    public Guid GameId { get; }

    /// <summary>
    /// Gets the number of players in the session.
    /// </summary>
    public int PlayerCount { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GameSessionCreatedEvent"/> class.
    /// </summary>
    public GameSessionCreatedEvent(Guid sessionId, Guid gameId, int playerCount)
    {
        SessionId = sessionId;
        GameId = gameId;
        PlayerCount = playerCount;
    }
}
