using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.Authentication.Domain.Events;

/// <summary>
/// Domain event raised when a user disables two-factor authentication.
/// </summary>
public sealed class TwoFactorDisabledEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the user who disabled 2FA.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TwoFactorDisabledEvent"/> class.
    /// </summary>
    public TwoFactorDisabledEvent(Guid userId)
    {
        UserId = userId;
    }
}
