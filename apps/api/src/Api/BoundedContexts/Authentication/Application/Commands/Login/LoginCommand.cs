using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Command to authenticate a user with email and password.
/// Returns LoginResponse with session token or temp token if 2FA required.
/// </summary>
internal record LoginCommand(
    string Email,
    string Password,
    string? IpAddress = null,
    string? UserAgent = null
) : ICommand<LoginResponse>;
