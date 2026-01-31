using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a game session is completed.
/// </summary>
internal sealed class GameSessionCompletedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the game session.
    /// </summary>
    public Guid SessionId { get; }

    /// <summary>
    /// Gets the ID of the game that was played.
    /// </summary>
    public Guid GameId { get; }

    /// <summary>
    /// Gets when the session was completed.
    /// </summary>
    public DateTime CompletedAt { get; }

    /// <summary>
    /// Gets the total duration of the session.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GameSessionCompletedEvent"/> class.
    /// </summary>
    public GameSessionCompletedEvent(Guid sessionId, Guid gameId, DateTime completedAt, TimeSpan duration)
    {
        SessionId = sessionId;
        GameId = gameId;
        CompletedAt = completedAt;
        Duration = duration;
    }
}
