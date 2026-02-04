using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.Authentication.Domain.Events;

/// <summary>
/// Domain event raised when a user account is locked due to excessive failed login attempts.
/// Issue #3339: Account lockout after failed login attempts.
/// </summary>
internal sealed class AccountLockedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the user whose account was locked.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// Gets the number of failed attempts that triggered the lockout.
    /// </summary>
    public int FailedAttempts { get; }

    /// <summary>
    /// Gets the timestamp when the lockout expires.
    /// </summary>
    public DateTime LockedUntil { get; }

    /// <summary>
    /// Gets the IP address of the last failed attempt (optional, for audit).
    /// </summary>
    public string? IpAddress { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountLockedEvent"/> class.
    /// </summary>
    public AccountLockedEvent(Guid userId, int failedAttempts, DateTime lockedUntil, string? ipAddress = null)
    {
        UserId = userId;
        FailedAttempts = failedAttempts;
        LockedUntil = lockedUntil;
        IpAddress = ipAddress;
    }
}
