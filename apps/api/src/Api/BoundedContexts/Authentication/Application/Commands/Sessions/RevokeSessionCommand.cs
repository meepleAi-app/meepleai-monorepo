using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Command with Result record
namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Command to revoke a specific session.
/// User must own the session OR have Admin role.
/// </summary>
internal record RevokeSessionCommand(
    Guid SessionId,
    Guid RequestingUserId,
    bool IsRequestingUserAdmin,
    string? Reason = null
) : ICommand<RevokeSessionResponse>;

/// <summary>
/// Response for RevokeSessionCommand.
/// </summary>
internal record RevokeSessionResponse(
    bool Success,
    string? ErrorMessage
);
