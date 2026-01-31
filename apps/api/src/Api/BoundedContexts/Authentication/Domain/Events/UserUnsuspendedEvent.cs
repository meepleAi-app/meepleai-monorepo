using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.Authentication.Domain.Events;

/// <summary>
/// Domain event raised when a user account is unsuspended (reactivated).
/// </summary>
internal sealed class UserUnsuspendedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the unsuspended user.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UserUnsuspendedEvent"/> class.
    /// </summary>
    public UserUnsuspendedEvent(Guid userId)
    {
        UserId = userId;
    }
}
