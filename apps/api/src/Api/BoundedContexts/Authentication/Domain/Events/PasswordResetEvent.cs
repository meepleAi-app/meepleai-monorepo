using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.Authentication.Domain.Events;

/// <summary>
/// Domain event raised when a user's password is reset by an administrator.
/// </summary>
internal sealed class PasswordResetEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the user whose password was reset.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// Gets the ID of the administrator who performed the reset.
    /// </summary>
    public Guid? ResetByUserId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PasswordResetEvent"/> class.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="resetByUserId">The administrator user ID who performed the reset</param>
    public PasswordResetEvent(Guid userId, Guid? resetByUserId = null)
    {
        UserId = userId;
        ResetByUserId = resetByUserId;
    }
}
