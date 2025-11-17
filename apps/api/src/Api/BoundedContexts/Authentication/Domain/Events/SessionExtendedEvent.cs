using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.Authentication.Domain.Events;

/// <summary>
/// Domain event raised when a user session is extended.
/// </summary>
public sealed class SessionExtendedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the extended session.
    /// </summary>
    public Guid SessionId { get; }

    /// <summary>
    /// Gets the ID of the user who owns the session.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// Gets the extension duration.
    /// </summary>
    public TimeSpan Extension { get; }

    /// <summary>
    /// Gets the new expiration date.
    /// </summary>
    public DateTime NewExpiresAt { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionExtendedEvent"/> class.
    /// </summary>
    public SessionExtendedEvent(Guid sessionId, Guid userId, TimeSpan extension, DateTime newExpiresAt)
    {
        SessionId = sessionId;
        UserId = userId;
        Extension = extension;
        NewExpiresAt = newExpiresAt;
    }
}
