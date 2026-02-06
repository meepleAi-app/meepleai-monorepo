using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a game session is automatically terminated (e.g., quota enforcement).
/// Issue #3671: Session limits enforcement with automatic termination.
/// </summary>
internal sealed class GameSessionTerminatedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the game session that was terminated.
    /// </summary>
    public Guid SessionId { get; }

    /// <summary>
    /// Gets the ID of the user who owned the terminated session.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// Gets the reason for termination (e.g., "QuotaExceeded").
    /// </summary>
    public string TerminationReason { get; }

    /// <summary>
    /// Gets when the session was terminated.
    /// </summary>
    public DateTime TerminatedAt { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GameSessionTerminatedEvent"/> class.
    /// </summary>
    public GameSessionTerminatedEvent(
        Guid sessionId,
        Guid userId,
        string terminationReason,
        DateTime terminatedAt)
    {
        SessionId = sessionId;
        UserId = userId;
        TerminationReason = terminationReason;
        TerminatedAt = terminatedAt;
    }
}
