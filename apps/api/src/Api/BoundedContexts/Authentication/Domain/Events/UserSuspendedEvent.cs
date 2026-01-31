using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.Authentication.Domain.Events;

/// <summary>
/// Domain event raised when a user account is suspended.
/// </summary>
internal sealed class UserSuspendedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the suspended user.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// Gets the reason for suspension.
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UserSuspendedEvent"/> class.
    /// </summary>
    public UserSuspendedEvent(Guid userId, string? reason = null)
    {
        UserId = userId;
        Reason = reason;
    }
}
