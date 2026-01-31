using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a game session is started.
/// </summary>
internal sealed class GameSessionStartedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the game session.
    /// </summary>
    public Guid SessionId { get; }

    /// <summary>
    /// Gets the ID of the game being played.
    /// </summary>
    public Guid GameId { get; }

    /// <summary>
    /// Gets when the session started.
    /// </summary>
    public DateTime StartedAt { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GameSessionStartedEvent"/> class.
    /// </summary>
    public GameSessionStartedEvent(Guid sessionId, Guid gameId, DateTime startedAt)
    {
        SessionId = sessionId;
        GameId = gameId;
        StartedAt = startedAt;
    }
}
