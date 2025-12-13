using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Command to logout from all devices (revoke all sessions) for the current user.
/// User-facing command with optional password verification and current session exclusion.
/// </summary>
/// <param name="UserId">The ID of the user requesting the logout.</param>
/// <param name="CurrentSessionTokenHash">Optional hash of the current session token to exclude from revocation.</param>
/// <param name="IncludeCurrentSession">Whether to also revoke the current session (requires confirmation).</param>
/// <param name="Password">Optional password for additional verification (security feature).</param>
public record LogoutAllDevicesCommand(
    Guid UserId,
    string? CurrentSessionTokenHash = null,
    bool IncludeCurrentSession = false,
    string? Password = null
) : ICommand<LogoutAllDevicesResult>;

/// <summary>
/// Result of the logout-all-devices operation.
/// </summary>
/// <param name="Success">Whether the operation succeeded.</param>
/// <param name="RevokedSessionCount">Number of sessions that were revoked.</param>
/// <param name="CurrentSessionRevoked">Whether the current session was also revoked.</param>
/// <param name="ErrorMessage">Error message if the operation failed.</param>
public record LogoutAllDevicesResult(
    bool Success,
    int RevokedSessionCount,
    bool CurrentSessionRevoked,
    string? ErrorMessage = null
);
