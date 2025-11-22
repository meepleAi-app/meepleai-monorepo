using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Command to authenticate a demo user without password validation.
/// Only works for users with IsDemoAccount flag set to true.
/// </summary>
public record DemoLoginCommand(
    string Email,
    string? IpAddress = null,
    string? UserAgent = null
) : ICommand<LoginResponse>;
