using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.Authentication.Domain.Events;

/// <summary>
/// Domain event raised when a user enables two-factor authentication.
/// </summary>
internal sealed class TwoFactorEnabledEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the user who enabled 2FA.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// Gets the number of backup codes generated.
    /// </summary>
    public int BackupCodesCount { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TwoFactorEnabledEvent"/> class.
    /// </summary>
    public TwoFactorEnabledEvent(Guid userId, int backupCodesCount)
    {
        UserId = userId;
        BackupCodesCount = backupCodesCount;
    }
}
