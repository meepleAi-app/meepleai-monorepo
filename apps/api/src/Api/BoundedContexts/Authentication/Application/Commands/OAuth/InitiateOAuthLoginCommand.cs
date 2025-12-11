using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Command with Result record
namespace Api.BoundedContexts.Authentication.Application.Commands.OAuth;

/// <summary>
/// Command to initiate OAuth 2.0 login flow.
/// Generates CSRF state token and returns authorization URL for redirection.
/// </summary>
public sealed record InitiateOAuthLoginCommand : ICommand<InitiateOAuthLoginResult>
{
    public string Provider { get; init; } = string.Empty;
    public string? IpAddress { get; init; }
}

/// <summary>
/// Result of OAuth login initiation.
/// Contains authorization URL for redirecting user to OAuth provider.
/// </summary>
public sealed record InitiateOAuthLoginResult
{
    public bool Success { get; init; }
    public string? AuthorizationUrl { get; init; }
    public string? ErrorMessage { get; init; }
}
