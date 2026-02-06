namespace Api.Infrastructure.Authorization;

/// <summary>
/// Confirmation level required for critical administrative actions.
/// Used to determine UI confirmation flow and audit log severity.
/// </summary>
public enum ConfirmationLevel
{
    /// <summary>
    /// No confirmation required
    /// </summary>
    None = 0,

    /// <summary>
    /// Warning modal with Cancel/Confirm buttons.
    /// Used for actions that can be undone or have limited impact.
    /// Examples: Clear cache pattern, edit user tier
    /// </summary>
    Level1 = 1,

    /// <summary>
    /// Critical action modal requiring typing "CONFIRM" to proceed.
    /// Used for irreversible actions or system-wide changes.
    /// Examples: Restart service, clear all cache, delete user, toggle global feature flags
    /// </summary>
    Level2 = 2
}
