using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Command with Result record
namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Command to extend a session's expiration time.
/// Rate limited to max 10 extensions per hour per user.
/// </summary>
internal record ExtendSessionCommand(
    Guid SessionId,
    Guid RequestingUserId,
    TimeSpan? ExtensionDuration = null // Defaults to 30 days if not specified
) : ICommand<ExtendSessionResponse>;

/// <summary>
/// Response for ExtendSessionCommand.
/// </summary>
internal record ExtendSessionResponse(
    bool Success,
    DateTime? NewExpiresAt,
    string? ErrorMessage
);
