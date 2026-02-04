using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.Authentication.Domain.Events;

/// <summary>
/// Domain event raised when a user account is unlocked (manually by admin or auto-unlock on successful login).
/// Issue #3339: Account lockout after failed login attempts.
/// </summary>
internal sealed class AccountUnlockedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the user whose account was unlocked.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// Gets whether this was a manual unlock by an admin (vs. automatic on successful login).
    /// </summary>
    public bool WasManualUnlock { get; }

    /// <summary>
    /// Gets the ID of the admin who unlocked the account (if manual unlock).
    /// </summary>
    public Guid? UnlockedByAdminId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountUnlockedEvent"/> class.
    /// </summary>
    public AccountUnlockedEvent(Guid userId, bool wasManualUnlock, Guid? unlockedByAdminId = null)
    {
        UserId = userId;
        WasManualUnlock = wasManualUnlock;
        UnlockedByAdminId = unlockedByAdminId;
    }
}
