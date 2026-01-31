using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Command with Result record
namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Command to create a new session for an authenticated user.
/// Used after OAuth callback or 2FA verification.
/// </summary>
internal record CreateSessionCommand(
    Guid UserId,
    string? IpAddress = null,
    string? UserAgent = null
) : ICommand<CreateSessionResponse>;

internal record CreateSessionResponse(
    UserDto User,
    string SessionToken,
    DateTime ExpiresAt
);
