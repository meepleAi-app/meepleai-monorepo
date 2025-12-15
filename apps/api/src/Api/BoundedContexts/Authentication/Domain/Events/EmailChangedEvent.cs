using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.Authentication.Domain.Events;

/// <summary>
/// Domain event raised when a user's email address is changed.
/// </summary>
internal sealed class EmailChangedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the user whose email was changed.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// Gets the old email address.
    /// </summary>
    public string OldEmail { get; }

    /// <summary>
    /// Gets the new email address.
    /// </summary>
    public string NewEmail { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailChangedEvent"/> class.
    /// </summary>
    public EmailChangedEvent(Guid userId, Email oldEmail, Email newEmail)
    {
        UserId = userId;
        OldEmail = oldEmail.Value;
        NewEmail = newEmail.Value;
    }
}
