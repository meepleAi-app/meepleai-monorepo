using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Command to register a new user.
/// Returns LoginResponse with session token for immediate authentication.
/// </summary>
public record RegisterCommand(
    string Email,
    string Password,
    string DisplayName,
    string? Role = null,
    string? IpAddress = null,
    string? UserAgent = null
) : ICommand<RegisterResponse>;

/// <summary>
/// Response for user registration.
/// Includes user details and session token for immediate login.
/// </summary>
public record RegisterResponse(
    UserDto User,
    string SessionToken,
    DateTime ExpiresAt
);
