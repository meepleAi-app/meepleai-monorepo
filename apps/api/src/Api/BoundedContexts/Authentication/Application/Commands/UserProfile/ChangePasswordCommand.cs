using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Command to change user password.
/// Requires current password verification before allowing change.
/// </summary>
internal record ChangePasswordCommand : ICommand
{
    public Guid UserId { get; init; }
    public string CurrentPassword { get; init; } = string.Empty;
    public string NewPassword { get; init; } = string.Empty;

    /// <summary>
    /// C7: ID of the session the change-password request is coming from. When
    /// non-null and <see cref="IncludeCurrentInRevoke"/> is false, every other
    /// session for the user is revoked but this one is preserved so the user
    /// stays logged in on the device they initiated the change from. When null,
    /// the handler falls back to revoking *every* session for safety.
    /// </summary>
    public Guid? CurrentSessionId { get; init; }

    /// <summary>
    /// C7: opt-in "logout everywhere" flag. When true the handler revokes
    /// every session for the user, including the one identified by
    /// <see cref="CurrentSessionId"/>. Used for the
    /// "I think my account is compromised" flow.
    /// </summary>
    public bool IncludeCurrentInRevoke { get; init; }
}
