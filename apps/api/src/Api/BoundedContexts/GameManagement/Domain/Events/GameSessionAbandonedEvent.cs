using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a game session is abandoned without completion.
/// </summary>
public sealed class GameSessionAbandonedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the game session.
    /// </summary>
    public Guid SessionId { get; }

    /// <summary>
    /// Gets when the session was abandoned.
    /// </summary>
    public DateTime AbandonedAt { get; }

    /// <summary>
    /// Gets the reason for abandonment.
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GameSessionAbandonedEvent"/> class.
    /// </summary>
    public GameSessionAbandonedEvent(Guid sessionId, DateTime abandonedAt, string? reason = null)
    {
        SessionId = sessionId;
        AbandonedAt = abandonedAt;
        Reason = reason;
    }
}
