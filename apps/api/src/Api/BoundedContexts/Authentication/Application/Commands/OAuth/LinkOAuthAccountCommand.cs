using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands.OAuth;

/// <summary>
/// Command to link an OAuth provider account to a user.
/// Enforces business rule: Only one account per provider is allowed.
/// </summary>
public sealed record LinkOAuthAccountCommand : ICommand<LinkOAuthAccountResult>
{
    public Guid UserId { get; init; }
    public string Provider { get; init; } = string.Empty;
    public string ProviderUserId { get; init; } = string.Empty;
    public string AccessTokenEncrypted { get; init; } = string.Empty;
    public string? RefreshTokenEncrypted { get; init; }
    public DateTime? TokenExpiresAt { get; init; }
}

/// <summary>
/// Result of OAuth account linking operation.
/// </summary>
public sealed record LinkOAuthAccountResult
{
    public Guid? OAuthAccountId { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}
