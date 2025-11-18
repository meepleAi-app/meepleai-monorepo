using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Command to revoke a specific session.
/// User must own the session OR have Admin role.
/// </summary>
public record RevokeSessionCommand(
    Guid SessionId,
    Guid RequestingUserId,
    bool IsRequestingUserAdmin,
    string? Reason = null
) : ICommand<RevokeSessionResponse>;

/// <summary>
/// Response for RevokeSessionCommand.
/// </summary>
public record RevokeSessionResponse(
    bool Success,
    string? ErrorMessage
);
