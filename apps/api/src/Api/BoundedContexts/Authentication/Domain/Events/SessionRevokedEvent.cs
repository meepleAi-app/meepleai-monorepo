using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.Authentication.Domain.Events;

/// <summary>
/// Domain event raised when a user session is revoked.
/// </summary>
public sealed class SessionRevokedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the revoked session.
    /// </summary>
    public Guid SessionId { get; }

    /// <summary>
    /// Gets the ID of the user who owned the session.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// Gets the reason for revocation.
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionRevokedEvent"/> class.
    /// </summary>
    public SessionRevokedEvent(Guid sessionId, Guid userId, string? reason = null)
    {
        SessionId = sessionId;
        UserId = userId;
        Reason = reason;
    }
}
