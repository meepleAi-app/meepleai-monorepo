using MediatR;

namespace Api.BoundedContexts.SessionTracking.Domain.Events;

/// <summary>
/// Domain event raised when a new session is created.
/// </summary>
public record SessionCreatedEvent : INotification
{
    /// <summary>
    /// Session unique identifier.
    /// </summary>
    public Guid SessionId { get; init; }

    /// <summary>
    /// User who created the session.
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// Unique session code.
    /// </summary>
    public string SessionCode { get; init; } = string.Empty;

    /// <summary>
    /// Optional game reference.
    /// </summary>
    public Guid? GameId { get; init; }

    /// <summary>
    /// When the session was created.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}