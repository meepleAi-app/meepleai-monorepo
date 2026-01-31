using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.Authentication.Domain.Events;

/// <summary>
/// Domain event raised when a user disables two-factor authentication.
/// </summary>
internal sealed class TwoFactorDisabledEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the user who disabled 2FA.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// Gets a value indicating whether this was an admin override (for locked-out users).
    /// </summary>
    public bool WasAdminOverride { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TwoFactorDisabledEvent"/> class.
    /// </summary>
    /// <param name="userId">The ID of the user who disabled 2FA.</param>
    /// <param name="wasAdminOverride">Whether this was an admin override operation.</param>
    public TwoFactorDisabledEvent(Guid userId, bool wasAdminOverride = false)
    {
        UserId = userId;
        WasAdminOverride = wasAdminOverride;
    }
}
