using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Command to revoke all sessions for a specific user (e.g., on password change).
/// </summary>
internal record RevokeAllUserSessionsCommand(Guid UserId) : ICommand<int>;
