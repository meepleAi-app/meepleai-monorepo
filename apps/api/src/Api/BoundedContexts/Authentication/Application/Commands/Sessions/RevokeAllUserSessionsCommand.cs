using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Command to revoke all sessions for a specific user (e.g., on password change).
/// </summary>
internal record RevokeAllUserSessionsCommand(
    Guid UserId,
    // #3838: the admin who forced the logout, for the security audit log.
    Guid? RevokedByUserId = null) : ICommand<int>;
