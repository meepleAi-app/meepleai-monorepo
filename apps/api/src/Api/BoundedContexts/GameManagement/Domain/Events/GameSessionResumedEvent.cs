using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a game session is resumed after being paused.
/// </summary>
public sealed class GameSessionResumedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the game session.
    /// </summary>
    public Guid SessionId { get; }

    /// <summary>
    /// Gets when the session was resumed.
    /// </summary>
    public DateTime ResumedAt { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GameSessionResumedEvent"/> class.
    /// </summary>
    public GameSessionResumedEvent(Guid sessionId, DateTime resumedAt)
    {
        SessionId = sessionId;
        ResumedAt = resumedAt;
    }
}
