using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.Authentication.Domain.Events;

/// <summary>
/// Domain event raised when a user changes their password.
/// </summary>
internal sealed class PasswordChangedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the user who changed their password.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PasswordChangedEvent"/> class.
    /// </summary>
    /// <param name="userId">The user ID</param>
    public PasswordChangedEvent(Guid userId)
    {
        UserId = userId;
    }
}
