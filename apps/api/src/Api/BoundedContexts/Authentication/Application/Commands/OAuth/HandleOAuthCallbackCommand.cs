using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Command with Result record
namespace Api.BoundedContexts.Authentication.Application.Commands.OAuth;

/// <summary>
/// Command to handle OAuth callback after user authorization.
/// Orchestrates token exchange, user creation/linking, and session establishment.
/// </summary>
public sealed record HandleOAuthCallbackCommand : ICommand<HandleOAuthCallbackResult>
{
    public string Provider { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
}

/// <summary>
/// Result of OAuth callback processing.
/// Contains user information and session token if successful.
/// </summary>
public sealed record HandleOAuthCallbackResult
{
    public Guid? UserId { get; init; }
    public bool IsNewUser { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? SessionToken { get; init; }
}
