using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Command with Result record
namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Command to register a new user.
/// Returns LoginResponse with session token for immediate authentication.
/// </summary>
internal record RegisterCommand(
    string Email,
    string Password,
    string DisplayName,
    string? Role = null,
    string? IpAddress = null,
    string? UserAgent = null,
    // C5: explicit bootstrap-admin token. When equal (constant-time) to the
    // configured Authentication:BootstrapAdminToken AND the single-use flag
    // hasn't been set yet, the resulting user is provisioned as Admin. All
    // other inputs land Role.User regardless of the registration order
    // (replaces the race-prone HasAnyUsersAsync first-user-is-admin path).
    string? BootstrapToken = null
) : ICommand<RegisterResponse>;

/// <summary>
/// Response for user registration.
/// Includes user details and session token for immediate login.
/// </summary>
internal record RegisterResponse(
    UserDto User,
    string SessionToken,
    DateTime ExpiresAt
);
