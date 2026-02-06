using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.Authentication.Domain.Events;

/// <summary>
/// Domain event raised when a user verifies their email address.
/// Issue #3672: Email verification flow.
/// </summary>
internal sealed class EmailVerifiedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the user who verified their email.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// Gets when the email was verified.
    /// </summary>
    public DateTime VerifiedAt { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailVerifiedEvent"/> class.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="verifiedAt">When the email was verified (UTC)</param>
    public EmailVerifiedEvent(Guid userId, DateTime verifiedAt)
    {
        UserId = userId;
        VerifiedAt = verifiedAt;
    }
}
