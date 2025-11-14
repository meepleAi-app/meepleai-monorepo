using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands.OAuth;

/// <summary>
/// Command to unlink an OAuth provider account from a user.
/// Enforces business rule: Cannot unlink if it's the only authentication method (prevents lockout).
/// </summary>
public sealed record UnlinkOAuthAccountCommand : ICommand<UnlinkOAuthAccountResult>
{
    public Guid UserId { get; init; }
    public string Provider { get; init; } = string.Empty;
}

/// <summary>
/// Result of OAuth account unlinking operation.
/// </summary>
public sealed record UnlinkOAuthAccountResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}
