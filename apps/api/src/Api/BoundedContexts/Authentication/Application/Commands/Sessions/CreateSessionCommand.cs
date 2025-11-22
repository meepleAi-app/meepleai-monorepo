using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Command to create a new session for an authenticated user.
/// Used after OAuth callback or 2FA verification.
/// </summary>
public record CreateSessionCommand(
    Guid UserId,
    string? IpAddress = null,
    string? UserAgent = null
) : ICommand<CreateSessionResponse>;

public record CreateSessionResponse(
    UserDto User,
    string SessionToken,
    DateTime ExpiresAt
);
