using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a game session is paused.
/// </summary>
internal sealed class GameSessionPausedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the game session.
    /// </summary>
    public Guid SessionId { get; }

    /// <summary>
    /// Gets when the session was paused.
    /// </summary>
    public DateTime PausedAt { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GameSessionPausedEvent"/> class.
    /// </summary>
    public GameSessionPausedEvent(Guid sessionId, DateTime pausedAt)
    {
        SessionId = sessionId;
        PausedAt = pausedAt;
    }
}
